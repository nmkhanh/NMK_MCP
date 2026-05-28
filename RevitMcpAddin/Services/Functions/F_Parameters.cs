using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Services
{
    public sealed partial class RevitService
    {
        private const int DefaultParameterBatchLimit = 100;
        private const int HardParameterBatchLimit = 500;

        public Task<object> GetElementParametersAsync(
            GetElementParametersRequest request,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var element = ResolveElement(doc, request.ElementId, "elementId");
                var instanceParameters = ReadParameters(element, "instance");
                var typeParameters = new List<RevitParameterInfo>();

                if (request.IncludeTypeParameters)
                {
                    var typeElement = GetTypeElement(element);
                    if (typeElement != null)
                        typeParameters = ReadParameters(typeElement, "type");
                }

                return Task.FromResult<object>(new
                {
                    success = true,
                    elementId = element.Id.ToString(),
                    elementName = element.Name,
                    instanceParameterCount = instanceParameters.Count,
                    typeParameterCount = typeParameters.Count,
                    parameters = new
                    {
                        instance = instanceParameters,
                        type = typeParameters
                    },
                    message = $"Read {instanceParameters.Count} instance parameter(s)" +
                              (request.IncludeTypeParameters ? $" and {typeParameters.Count} type parameter(s)." : ".")
                });
            }, ct);
        }

        public Task<object> SetElementParameterAsync(
            SetElementParameterRequest request,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var element = ResolveElement(doc, request.ElementId, "elementId");
                if (string.IsNullOrWhiteSpace(request.ParameterName))
                    throw new ArgumentException("'parameterName' is required.");

                ParameterWriteResult result;
                using (var tx = new Transaction(doc, "RevitMCP: Set Element Parameter"))
                {
                    tx.Start();
                    result = TrySetParameter(element, request.ParameterName, request.Value, request.Target);
                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                Logger.Info($"Parameter set: element={element.Id}, parameter={request.ParameterName}, success={result.Success}");

                return Task.FromResult<object>(new
                {
                    success = result.Success,
                    elementId = element.Id.ToString(),
                    result,
                    message = result.Success
                        ? $"Parameter '{request.ParameterName}' updated on element {element.Id}."
                        : $"Parameter '{request.ParameterName}' was not updated on element {element.Id}: {result.Error}"
                });
            }, ct);
        }

        public Task<object> SetElementParametersAsync(
            SetElementParametersRequest request,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                var element = ResolveElement(doc, request.ElementId, "elementId");
                if (request.Parameters.Count == 0)
                    throw new ArgumentException("'parameters' must contain at least one parameter.");

                var results = new List<ParameterWriteResult>();
                using (var tx = new Transaction(doc, "RevitMCP: Set Element Parameters"))
                {
                    tx.Start();
                    foreach (var (name, value) in request.Parameters)
                        results.Add(TrySetParameter(element, name, value, request.Target));

                    var status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                }

                var successCount = results.Count(r => r.Success);

                return Task.FromResult<object>(new
                {
                    success = successCount == results.Count,
                    elementId = element.Id.ToString(),
                    requestedCount = results.Count,
                    successCount,
                    failureCount = results.Count - successCount,
                    results,
                    message = $"Updated {successCount} of {results.Count} parameter(s) on element {element.Id}."
                });
            }, ct);
        }

        public Task<object> BatchSetParametersAsync(
            BatchSetParametersRequest request,
            CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                if (request.ElementIds.Count == 0)
                    throw new ArgumentException("'elementIds' must contain at least one id.");
                if (request.Parameters.Count == 0)
                    throw new ArgumentException("'parameters' must contain at least one parameter.");

                var maxItems = request.MaxItems <= 0 ? DefaultParameterBatchLimit : request.MaxItems;
                maxItems = Math.Min(maxItems, HardParameterBatchLimit);
                if (request.ElementIds.Count > maxItems)
                    throw new ArgumentException(
                        $"Batch contains {request.ElementIds.Count} element(s), above maxItems={maxItems}. " +
                        $"Increase maxItems up to {HardParameterBatchLimit} or split the batch.");

                var elementResults = new List<object>();
                var totalWrites = 0;
                var successfulWrites = 0;

                Transaction? tx = null;
                try
                {
                    if (!request.DryRun)
                    {
                        tx = new Transaction(doc, "RevitMCP: Batch Set Parameters");
                        tx.Start();
                    }

                    foreach (var rawId in request.ElementIds)
                    {
                        var perElement = new List<ParameterWriteResult>();
                        Element? element = null;
                        string? elementError = null;

                        try
                        {
                            element = ResolveElement(doc, rawId, "elementId");
                            foreach (var (name, value) in request.Parameters)
                            {
                                var writeResult = request.DryRun
                                    ? PreviewSetParameter(element, name, request.Target)
                                    : TrySetParameter(element, name, value, request.Target);

                                perElement.Add(writeResult);
                                totalWrites++;
                                if (writeResult.Success) successfulWrites++;
                            }
                        }
                        catch (Exception ex)
                        {
                            elementError = ex.Message;
                        }

                        elementResults.Add(new
                        {
                            elementId = rawId,
                            resolvedElementId = element?.Id.ToString(),
                            success = elementError == null && perElement.All(r => r.Success),
                            error = elementError,
                            results = perElement
                        });
                    }

                    if (tx != null)
                    {
                        var status = tx.Commit();
                        if (status != TransactionStatus.Committed)
                            throw new InvalidOperationException($"Transaction did not commit (status={status}).");
                    }
                }
                catch
                {
                    if (tx != null && tx.HasStarted())
                        tx.RollBack();
                    throw;
                }
                finally
                {
                    tx?.Dispose();
                }

                return Task.FromResult<object>(new
                {
                    success = successfulWrites == totalWrites && elementResults.Count > 0,
                    dryRun = request.DryRun,
                    elementCount = request.ElementIds.Count,
                    parameterCount = request.Parameters.Count,
                    totalWrites,
                    successfulWrites,
                    failedWrites = totalWrites - successfulWrites,
                    elements = elementResults,
                    message = request.DryRun
                        ? $"Dry run checked {totalWrites} parameter write(s)."
                        : $"Updated {successfulWrites} of {totalWrites} parameter write(s)."
                });
            }, ct, timeoutMs: 120_000);
        }

        private static Element ResolveElement(Document doc, string? rawId, string paramName)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                throw new ArgumentException($"'{paramName}' is required.");

            if (!long.TryParse(rawId.Trim(), out var id))
                throw new ArgumentException($"Invalid {paramName} '{rawId}'. Expected an integer Revit ElementId.");

            var element = doc.GetElement(new ElementId(id));
            return element ?? throw new InvalidOperationException($"Element id '{rawId}' was not found.");
        }

        private static Element? GetTypeElement(Element element)
        {
            try
            {
                var typeId = element.GetTypeId();
                return typeId == ElementId.InvalidElementId ? null : element.Document.GetElement(typeId);
            }
            catch
            {
                return null;
            }
        }

        private static List<RevitParameterInfo> ReadParameters(Element element, string source)
        {
            var result = new List<RevitParameterInfo>();
            foreach (Parameter p in element.Parameters)
            {
                var name = p.Definition?.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                result.Add(BuildParameterInfo(p, source));
            }
            return result.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static RevitParameterInfo BuildParameterInfo(Parameter p, string source)
        {
            return new RevitParameterInfo
            {
                Name = p.Definition?.Name ?? string.Empty,
                StorageType = p.StorageType.ToString(),
                IsReadOnly = p.IsReadOnly,
                IsShared = p.IsShared,
                HasValue = p.HasValue,
                Value = GetParameterRawValue(p),
                DisplayValue = GetParameterDisplayValue(p),
                Source = source
            };
        }

        private static object? GetParameterRawValue(Parameter p)
        {
            if (!p.HasValue) return null;
            return p.StorageType switch
            {
                StorageType.String => p.AsString(),
                StorageType.Integer => p.AsInteger(),
                StorageType.Double => p.AsDouble(),
                StorageType.ElementId => p.AsElementId().ToString(),
                _ => null
            };
        }

        private static string GetParameterDisplayValue(Parameter p)
        {
            if (!p.HasValue) return string.Empty;
            return p.AsValueString() ?? p.AsString() ?? GetParameterRawValue(p)?.ToString() ?? string.Empty;
        }

        private static ParameterWriteResult PreviewSetParameter(Element element, string parameterName, string target)
        {
            var found = TryFindWritableParameter(element, parameterName, target, out var parameter, out var source, out var error);
            return new ParameterWriteResult
            {
                ParameterName = parameterName,
                Source = source,
                StorageType = parameter?.StorageType.ToString() ?? string.Empty,
                OldValue = parameter != null ? GetParameterDisplayValue(parameter) : null,
                NewValue = null,
                Success = found,
                DryRun = true,
                Error = error
            };
        }

        private static ParameterWriteResult TrySetParameter(
            Element element,
            string parameterName,
            JToken? value,
            string target)
        {
            var result = PreviewSetParameter(element, parameterName, target);
            result.DryRun = false;
            if (!result.Success)
                return result;

            TryFindWritableParameter(element, parameterName, target, out var parameter, out _, out _);
            if (parameter == null)
            {
                result.Success = false;
                result.Error = $"Parameter '{parameterName}' was not found.";
                return result;
            }

            try
            {
                SetParameterValue(parameter, value);
                result.NewValue = GetParameterDisplayValue(parameter);
                result.Success = true;
                result.Error = null;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private static bool TryFindWritableParameter(
            Element element,
            string parameterName,
            string target,
            out Parameter? parameter,
            out string source,
            out string? error)
        {
            parameter = null;
            source = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                error = "Parameter name is required.";
                return false;
            }

            var normalizedTarget = (target ?? "instance").Trim().ToLowerInvariant();
            if (normalizedTarget is not ("instance" or "type"))
            {
                error = "target must be 'instance' or 'type'.";
                return false;
            }

            Element searchElement = element;
            source = "instance";
            if (normalizedTarget == "type")
            {
                searchElement = GetTypeElement(element) ??
                    throw new InvalidOperationException($"Element {element.Id} has no editable type element.");
                source = "type";
            }

            parameter = searchElement.LookupParameter(parameterName.Trim());
            if (parameter == null)
            {
                error = $"Parameter '{parameterName}' was not found on {source} parameters.";
                return false;
            }

            if (parameter.IsReadOnly)
            {
                error = $"Parameter '{parameterName}' is read-only.";
                return false;
            }

            return true;
        }

        private static void SetParameterValue(Parameter parameter, JToken? value)
        {
            if (value == null || value.Type == JTokenType.Null)
                throw new ArgumentException("Parameter value cannot be null.");

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    parameter.Set(value.Type == JTokenType.String ? value.Value<string>() : value.ToString());
                    break;

                case StorageType.Integer:
                    parameter.Set(ToIntegerParameterValue(value));
                    break;

                case StorageType.Double:
                    parameter.Set(ToDoubleParameterValue(parameter, value));
                    break;

                case StorageType.ElementId:
                    parameter.Set(new ElementId(ToLongParameterValue(value)));
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported parameter storage type '{parameter.StorageType}'.");
            }
        }

        private static int ToIntegerParameterValue(JToken value)
        {
            if (value.Type == JTokenType.Boolean)
                return value.Value<bool>() ? 1 : 0;

            if (value.Type == JTokenType.Integer)
                return value.Value<int>();

            var text = value.Value<string>() ?? value.ToString();
            if (bool.TryParse(text, out var boolValue))
                return boolValue ? 1 : 0;
            if (int.TryParse(text, out var intValue))
                return intValue;

            throw new ArgumentException($"Cannot convert '{text}' to integer parameter value.");
        }

        private static long ToLongParameterValue(JToken value)
        {
            if (value.Type == JTokenType.Integer)
                return value.Value<long>();

            var text = value.Value<string>() ?? value.ToString();
            if (long.TryParse(text, out var longValue))
                return longValue;

            throw new ArgumentException($"Cannot convert '{text}' to ElementId parameter value.");
        }

        private static double ToDoubleParameterValue(Parameter parameter, JToken value)
        {
            if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                return value.Value<double>();

            var text = value.Value<string>() ?? value.ToString();
            if (parameter.SetValueString(text))
                return parameter.AsDouble();
            if (double.TryParse(text, out var doubleValue))
                return doubleValue;

            throw new ArgumentException($"Cannot convert '{text}' to double parameter value.");
        }

        private sealed class ParameterWriteResult
        {
            public string ParameterName { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public string StorageType { get; set; } = string.Empty;
            public string? OldValue { get; set; }
            public string? NewValue { get; set; }
            public bool Success { get; set; }
            public bool DryRun { get; set; }
            public string? Error { get; set; }
        }
    }
}

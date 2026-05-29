using Newtonsoft.Json.Linq;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    public static class RebarHandlerRegistration
    {
        public static McpRouter RegisterRebarHandlers(this McpRouter router, RevitService service)
        {
            return router
                .Register(new RebarToolHandler<RebarListRequest>(
                    service,
                    "get_rebars",
                    "Lists Rebar elements with optional host filtering.",
                    Schemas.RebarList,
                    (svc, req, ct) => svc.GetRebarsAsync(req, ct)))
                .Register(new RebarToolHandler<RebarHostCandidatesRequest>(
                    service,
                    "get_rebar_host_candidates",
                    "Lists concrete/structural elements that can host reinforcement.",
                    Schemas.HostCandidates,
                    (svc, req, ct) => svc.GetRebarHostCandidatesAsync(req, ct)))
                .Register(new RebarToolHandler<RebarTypeListRequest>(
                    service,
                    "get_rebar_bar_types",
                    "Lists available RebarBarType elements.",
                    Schemas.TypeList,
                    (svc, req, ct) => svc.GetRebarBarTypesAsync(req, ct)))
                .Register(new RebarToolHandler<RebarTypeListRequest>(
                    service,
                    "get_rebar_shapes",
                    "Lists available RebarShape elements.",
                    Schemas.TypeList,
                    (svc, req, ct) => svc.GetRebarShapesAsync(req, ct)))
                .Register(new RebarToolHandler<RebarTypeListRequest>(
                    service,
                    "get_rebar_hook_types",
                    "Lists available RebarHookType elements.",
                    Schemas.TypeList,
                    (svc, req, ct) => svc.GetRebarHookTypesAsync(req, ct)))
                .Register(new RebarToolHandler<RebarTypeListRequest>(
                    service,
                    "get_rebar_cover_types",
                    "Lists rebar cover types and distances.",
                    Schemas.TypeList,
                    (svc, req, ct) => svc.GetRebarCoverTypesAsync(req, ct)))
                .Register(new RebarToolHandler<RebarElementRequest>(
                    service,
                    "get_rebar_constraints",
                    "Reads a rebar constraint/accessor summary.",
                    Schemas.ElementId,
                    (svc, req, ct) => svc.GetRebarConstraintsAsync(req, ct)))
                .Register(new RebarToolHandler<RebarElementRequest>(
                    service,
                    "get_rebar_centerline_curves",
                    "Reads centerline curves for a rebar.",
                    Schemas.ElementId,
                    (svc, req, ct) => svc.GetRebarCenterlineCurvesAsync(req, ct)))
                .Register(new RebarToolHandler<CreateRebarFromCurvesRequest>(
                    service,
                    "create_rebar_from_curves",
                    "Creates shape-driven rebar from line curves on a valid host.",
                    Schemas.CreateRebarFromCurves,
                    (svc, req, ct) => svc.CreateRebarFromCurvesAsync(req, ct)))
                .Register(new RebarToolHandler<CreateRebarFromShapeRequest>(
                    service,
                    "create_rebar_from_shape",
                    "Creates shape-driven rebar from an existing RebarShape.",
                    Schemas.CreateRebarFromShape,
                    (svc, req, ct) => svc.CreateRebarFromShapeAsync(req, ct)))
                .Register(new RebarToolHandler<RebarLayoutRequest>(
                    service,
                    "update_rebar_layout",
                    "Updates shape-driven rebar layout rule, count, spacing, and array length.",
                    Schemas.RebarLayout,
                    (svc, req, ct) => svc.UpdateRebarLayoutAsync(req, ct)))
                .Register(new RebarToolHandler<RebarHooksRequest>(
                    service,
                    "update_rebar_hooks",
                    "Updates start/end hook type ids for a rebar.",
                    Schemas.RebarHooks,
                    (svc, req, ct) => svc.UpdateRebarHooksAsync(req, ct)))
                .Register(new RebarToolHandler<RebarConstraintsRequest>(
                    service,
                    "update_rebar_constraints",
                    "Updates supported rebar constraint options and recomputes when available.",
                    Schemas.RebarConstraints,
                    (svc, req, ct) => svc.UpdateRebarConstraintsAsync(req, ct)))
                .Register(new RebarToolHandler<RebarCoverRequest>(
                    service,
                    "set_rebar_cover",
                    "Sets the common rebar cover type on a host.",
                    Schemas.RebarCover,
                    (svc, req, ct) => svc.SetRebarCoverAsync(req, ct)))
                .Register(new RebarToolHandler<RebarVisibilityRequest>(
                    service,
                    "set_rebar_visibility_in_view",
                    "Controls rebar/coupler visibility in a Revit view.",
                    Schemas.RebarVisibility,
                    (svc, req, ct) => svc.SetRebarVisibilityInViewAsync(req, ct)))
                .Register(new RebarToolHandler<RebarIdsRequest>(
                    service,
                    "delete_rebars",
                    "Deletes Rebar elements by id, with dry-run support.",
                    Schemas.ElementIds,
                    (svc, req, ct) => svc.DeleteRebarsAsync(req, ct)))
                .Register(new RebarToolHandler<RebarSystemRequest>(
                    service,
                    "create_area_reinforcement",
                    "Creates AreaReinforcement from curves, boundary points, or host bounds.",
                    Schemas.RebarSystem,
                    (svc, req, ct) => svc.CreateAreaReinforcementAsync(req, ct)))
                .Register(new RebarToolHandler<UpdateRebarSystemRequest>(
                    service,
                    "update_area_reinforcement",
                    "Updates type/parameters on an AreaReinforcement element.",
                    Schemas.UpdateRebarSystem,
                    (svc, req, ct) => svc.UpdateAreaReinforcementAsync(req, ct)))
                .Register(new RebarToolHandler<RebarSystemRequest>(
                    service,
                    "create_path_reinforcement",
                    "Creates PathReinforcement from curves or host bounds.",
                    Schemas.RebarSystem,
                    (svc, req, ct) => svc.CreatePathReinforcementAsync(req, ct)))
                .Register(new RebarToolHandler<UpdateRebarSystemRequest>(
                    service,
                    "update_path_reinforcement",
                    "Updates type/parameters on a PathReinforcement element.",
                    Schemas.UpdateRebarSystem,
                    (svc, req, ct) => svc.UpdatePathReinforcementAsync(req, ct)))
                .Register(new RebarToolHandler<RebarSystemRequest>(
                    service,
                    "create_fabric_area",
                    "Creates FabricArea from host bounds or boundary loops.",
                    Schemas.RebarSystem,
                    (svc, req, ct) => svc.CreateFabricAreaAsync(req, ct)))
                .Register(new RebarToolHandler<UpdateRebarSystemRequest>(
                    service,
                    "update_fabric_area",
                    "Updates type/parameters on a FabricArea element.",
                    Schemas.UpdateRebarSystem,
                    (svc, req, ct) => svc.UpdateFabricAreaAsync(req, ct)))
                .Register(new RebarToolHandler<RebarSystemRequest>(
                    service,
                    "place_fabric_sheet",
                    "Places a FabricSheet on a valid host.",
                    Schemas.RebarSystem,
                    (svc, req, ct) => svc.PlaceFabricSheetAsync(req, ct)))
                .Register(new RebarToolHandler<UpdateRebarSystemRequest>(
                    service,
                    "update_fabric_sheet",
                    "Updates type/parameters on a FabricSheet element.",
                    Schemas.UpdateRebarSystem,
                    (svc, req, ct) => svc.UpdateFabricSheetAsync(req, ct)))
                .Register(new RebarToolHandler<RebarTypeListRequest>(
                    service,
                    "get_rebar_coupler_types",
                    "Lists rebar coupler type candidates.",
                    Schemas.TypeList,
                    (svc, req, ct) => svc.GetRebarCouplerTypesAsync(req, ct)))
                .Register(new RebarToolHandler<RebarListRequest>(
                    service,
                    "get_rebar_couplers",
                    "Lists RebarCoupler elements.",
                    Schemas.RebarList,
                    (svc, req, ct) => svc.GetRebarCouplersAsync(req, ct)))
                .Register(new RebarToolHandler<RebarElementRequest>(
                    service,
                    "get_rebar_coupler",
                    "Reads one RebarCoupler by id.",
                    Schemas.ElementId,
                    (svc, req, ct) => svc.GetRebarCouplerAsync(req, ct)))
                .Register(new RebarToolHandler<CreateRebarCouplerRequest>(
                    service,
                    "create_rebar_coupler",
                    "Creates a RebarCoupler on one or two rebar ends.",
                    Schemas.CreateCoupler,
                    (svc, req, ct) => svc.CreateRebarCouplerAsync(req, ct)))
                .Register(new RebarToolHandler<UpdateRebarCouplerRequest>(
                    service,
                    "update_rebar_coupler",
                    "Updates mark, rotation, and parameters on a RebarCoupler.",
                    Schemas.UpdateCoupler,
                    (svc, req, ct) => svc.UpdateRebarCouplerAsync(req, ct)))
                .Register(new RebarToolHandler<ChangeRebarCouplerTypeRequest>(
                    service,
                    "change_rebar_coupler_type",
                    "Changes the type of a RebarCoupler.",
                    Schemas.ChangeCouplerType,
                    (svc, req, ct) => svc.ChangeRebarCouplerTypeAsync(req, ct)))
                .Register(new RebarToolHandler<RebarIdsRequest>(
                    service,
                    "delete_rebar_couplers",
                    "Deletes RebarCoupler elements by id, with dry-run support.",
                    Schemas.ElementIds,
                    (svc, req, ct) => svc.DeleteRebarCouplersAsync(req, ct)))
                .Register(new RebarToolHandler<RebarTypeListRequest>(
                    service,
                    "get_rebar_end_treatments",
                    "Lists EndTreatmentType elements.",
                    Schemas.TypeList,
                    (svc, req, ct) => svc.GetRebarEndTreatmentsAsync(req, ct)))
                .Register(new RebarToolHandler<EndTreatmentRequest>(
                    service,
                    "set_rebar_end_treatment",
                    "Sets an end treatment type on one rebar end.",
                    Schemas.EndTreatment,
                    (svc, req, ct) => svc.SetRebarEndTreatmentAsync(req, ct)))
                .Register(new RebarToolHandler<CreateRebarCouplerRequest>(
                    service,
                    "validate_rebar_coupler_placement",
                    "Validates rebar coupler placement inputs without creating a coupler.",
                    Schemas.CreateCoupler,
                    (svc, req, ct) => svc.ValidateRebarCouplerPlacementAsync(req, ct)))
                .Register(new RebarToolHandler<RebarTagRequest>(
                    service,
                    "create_rebar_tag",
                    "Creates an IndependentTag for a rebar/reinforcement element.",
                    Schemas.RebarTag,
                    (svc, req, ct) => svc.CreateRebarTagAsync(req, ct)))
                .Register(new RebarToolHandler<MultiRebarAnnotationRequest>(
                    service,
                    "create_multi_rebar_annotation",
                    "Creates a MultiReferenceAnnotation for one or more rebars.",
                    Schemas.MultiRebarAnnotation,
                    (svc, req, ct) => svc.CreateMultiRebarAnnotationAsync(req, ct)))
                .Register(new RebarToolHandler<RebarScheduleRequest>(
                    service,
                    "create_rebar_schedule",
                    "Creates a Rebar schedule with optional fields.",
                    Schemas.RebarSchedule,
                    (svc, req, ct) => svc.CreateRebarScheduleAsync(req, ct)))
                .Register(new RebarToolHandler<RebarQuantityRequest>(
                    service,
                    "get_rebar_quantities",
                    "Returns bar and coupler quantity summaries.",
                    Schemas.RebarQuantities,
                    (svc, req, ct) => svc.GetRebarQuantitiesAsync(req, ct)))
                .Register(new RebarToolHandler<SetRebarPartitionRequest>(
                    service,
                    "set_rebar_partition",
                    "Sets the Partition parameter on rebar/coupler elements.",
                    Schemas.SetPartition,
                    (svc, req, ct) => svc.SetRebarPartitionAsync(req, ct)))
                .RegisterWorkflow(
                    service,
                    "create_column_vertical_rebars",
                    "Creates vertical column rebars from the host bounding box.",
                    (svc, req, ct) => svc.CreateColumnVerticalRebarsAsync(req, ct))
                .RegisterWorkflow(
                    service,
                    "create_column_ties",
                    "Creates column tie/stirrup rebars from the host bounding box.",
                    (svc, req, ct) => svc.CreateColumnTiesAsync(req, ct))
                .RegisterWorkflow(
                    service,
                    "create_beam_longitudinal_rebars",
                    "Creates longitudinal beam rebars from the host bounding box.",
                    (svc, req, ct) => svc.CreateBeamLongitudinalRebarsAsync(req, ct))
                .RegisterWorkflow(
                    service,
                    "create_beam_stirrups",
                    "Creates beam stirrups from the host bounding box.",
                    (svc, req, ct) => svc.CreateBeamStirrupsAsync(req, ct))
                .RegisterWorkflow(
                    service,
                    "create_wall_rebar_grid",
                    "Creates a wall rebar grid from the host bounding box.",
                    (svc, req, ct) => svc.CreateWallRebarGridAsync(req, ct))
                .RegisterWorkflow(
                    service,
                    "create_slab_rebar_grid",
                    "Creates a slab rebar grid from the host bounding box.",
                    (svc, req, ct) => svc.CreateSlabRebarGridAsync(req, ct));
        }

        private static McpRouter RegisterWorkflow(
            this McpRouter router,
            RevitService service,
            string name,
            string description,
            Func<RevitService, RebarWorkflowRequest, CancellationToken, Task<object>> execute)
        {
            return router.Register(new RebarToolHandler<RebarWorkflowRequest>(
                service,
                name,
                description,
                Schemas.Workflow,
                execute));
        }
    }

    internal sealed class RebarToolHandler<TRequest> : IToolHandler
        where TRequest : class, new()
    {
        private readonly RevitService _revitService;
        private readonly string _description;
        private readonly object _inputSchema;
        private readonly Func<RevitService, TRequest, CancellationToken, Task<object>> _execute;

        public RebarToolHandler(
            RevitService revitService,
            string toolName,
            string description,
            object inputSchema,
            Func<RevitService, TRequest, CancellationToken, Task<object>> execute)
        {
            _revitService = revitService;
            ToolName = toolName;
            _description = description;
            _inputSchema = inputSchema;
            _execute = execute;
        }

        public string ToolName { get; }

        public McpToolDefinition GetDefinition() => new()
        {
            Name = ToolName,
            Description = _description,
            InputSchema = _inputSchema
        };

        public async Task<ToolHandlerResult> HandleAsync(
            JObject? arguments,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = arguments?.ToObject<TRequest>() ?? new TRequest();
                return ToolHandlerResult.FromJson(await _execute(_revitService, request, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ToolName} handler error", ex);
                return ToolHandlerResult.FromError($"Failed to run {ToolName}: {ex.Message}");
            }
        }
    }

    internal static class Schemas
    {
        private static object Object(Dictionary<string, object> properties, params string[] required)
            => new
            {
                type = "object",
                properties,
                required
            };

        private static Dictionary<string, object> Props(params (string Name, object Value)[] props)
            => props.ToDictionary(p => p.Name, p => p.Value);

        private static object String(string description)
            => new { type = "string", description };

        private static object Number(string description)
            => new { type = "number", description };

        private static object Integer(string description)
            => new { type = "integer", description };

        private static object Boolean(string description)
            => new { type = "boolean", description };

        private static object UseActiveView
            => new
            {
                type = "boolean",
                description = "When true, scope the query to elements visible in the active view. Default: false.",
                @default = false
            };

        private static object QueryViewId
            => String("Optional view ElementId to scope the query. Overrides useActiveView when supplied.");

        private static object StringArray(string description)
            => new { type = "array", description, items = new { type = "string" } };

        private static object Point => Object(Props(
            ("x", Number("X coordinate.")),
            ("y", Number("Y coordinate.")),
            ("z", Number("Z coordinate."))), "x", "y", "z");

        private static object Curve => Object(Props(
            ("start", Point),
            ("end", Point)), "start", "end");

        private static object Curves => new
        {
            type = "array",
            description = "Line curves as start/end point pairs.",
            items = Curve
        };

        private static object Parameters => new
        {
            type = "object",
            description = "Optional instance parameter values keyed by parameter name.",
            additionalProperties = true
        };

        public static object RebarList => Object(Props(
            ("hostId", String("Optional host ElementId.")),
            ("kind", String("Optional reinforcement kind hint.")),
            ("includeParameters", Boolean("Include instance parameters.")),
            ("maxItems", Integer("Maximum items to return.")),
            ("useActiveView", UseActiveView),
            ("viewId", QueryViewId)));

        public static object HostCandidates => Object(Props(
            ("category", String("Optional BuiltInCategory suffix, e.g. StructuralColumns.")),
            ("maxItems", Integer("Maximum items to return.")),
            ("useActiveView", UseActiveView),
            ("viewId", QueryViewId)));

        public static object TypeList => Object(Props(
            ("nameContains", String("Optional case-insensitive name filter.")),
            ("maxItems", Integer("Maximum items to return."))));

        public static object ElementId => Object(Props(
            ("elementId", String("Revit ElementId.")),
            ("includeParameters", Boolean("Include instance parameters."))), "elementId");

        public static object CreateRebarFromCurves => Object(Props(
            ("hostId", String("Valid rebar host ElementId.")),
            ("barTypeId", String("Optional RebarBarType ElementId. Defaults to first available type.")),
            ("startHookTypeId", String("Optional start RebarHookType ElementId.")),
            ("endHookTypeId", String("Optional end RebarHookType ElementId.")),
            ("style", String("standard or stirrup_tie.")),
            ("startHookOrientation", String("left or right.")),
            ("endHookOrientation", String("left or right.")),
            ("normalX", Number("Rebar plane normal X.")),
            ("normalY", Number("Rebar plane normal Y.")),
            ("normalZ", Number("Rebar plane normal Z.")),
            ("curves", Curves),
            ("unit", String("feet, meters, or millimeters.")),
            ("useExistingShapeIfPossible", Boolean("Reuse a matching RebarShape when possible.")),
            ("createNewShape", Boolean("Create a new RebarShape if needed.")),
            ("layoutRule", String("Optional layout rule.")),
            ("count", Integer("Optional bar count.")),
            ("spacing", Number("Optional spacing.")),
            ("arrayLength", Number("Optional array length.")),
            ("parameters", Parameters)), "hostId", "curves");

        public static object CreateRebarFromShape => Object(Props(
            ("hostId", String("Valid rebar host ElementId.")),
            ("shapeId", String("RebarShape ElementId.")),
            ("barTypeId", String("Optional RebarBarType ElementId.")),
            ("origin", Point),
            ("xVector", Point),
            ("yVector", Point),
            ("unit", String("feet, meters, or millimeters.")),
            ("parameters", Parameters)), "hostId", "shapeId");

        public static object RebarLayout => Object(Props(
            ("rebarId", String("Rebar ElementId.")),
            ("layoutRule", String("single, number_with_spacing, fixed_number, maximum_spacing, or minimum_clear_spacing.")),
            ("count", Integer("Optional bar count.")),
            ("spacing", Number("Optional spacing.")),
            ("arrayLength", Number("Optional array length.")),
            ("barsOnNormalSide", Boolean("Bars on normal side.")),
            ("includeFirstBar", Boolean("Include first bar.")),
            ("includeLastBar", Boolean("Include last bar.")),
            ("unit", String("feet, meters, or millimeters."))), "rebarId", "layoutRule");

        public static object RebarHooks => Object(Props(
            ("rebarId", String("Rebar ElementId.")),
            ("startHookTypeId", String("Optional start hook type ElementId.")),
            ("endHookTypeId", String("Optional end hook type ElementId."))), "rebarId");

#if R26
        public static object RebarConstraints => Object(Props(
            ("rebarId", String("Rebar ElementId.")),
            ("useRebarConstraintsToProduceVaryingBars", Boolean("Toggle varying bars driven by constraints."))), "rebarId");
#else
        public static object RebarConstraints => Object(Props(
            ("rebarId", String("Rebar ElementId."))), "rebarId");
#endif

        public static object RebarCover => Object(Props(
            ("hostId", String("Valid rebar host ElementId.")),
            ("coverTypeId", String("Optional RebarCoverType ElementId. Defaults to first available type."))), "hostId");

        public static object RebarVisibility => Object(Props(
            ("elementId", String("Rebar, AreaReinforcement, or RebarCoupler ElementId.")),
            ("viewId", String("Optional view ElementId. Defaults to active view.")),
            ("unobscured", Boolean("Show unobscured in view.")),
            ("presentationMode", String("Optional RebarPresentationMode.")),
            ("barIndex", Integer("Optional bar index for hidden status.")),
            ("hidden", Boolean("Bar hidden status."))), "elementId");

        public static object ElementIds => Object(Props(
            ("elementIds", StringArray("ElementIds to process.")),
            ("dryRun", Boolean("Validate without modifying the document.")),
            ("maxItems", Integer("Maximum items allowed."))), "elementIds");

        public static object RebarSystem => Object(Props(
            ("hostId", String("Valid rebar host ElementId.")),
            ("typeId", String("Area/path/fabric type ElementId.")),
            ("barTypeId", String("RebarBarType or fabric sheet type ElementId depending on tool.")),
            ("hookTypeId", String("Optional RebarHookType ElementId.")),
            ("startHookTypeId", String("Optional start hook type ElementId.")),
            ("endHookTypeId", String("Optional end hook type ElementId.")),
            ("fabricSheetTypeId", String("Optional FabricSheetType ElementId.")),
            ("curves", Curves),
            ("boundary", new { type = "array", description = "Boundary points.", items = Point }),
            ("directionX", Number("Major direction X.")),
            ("directionY", Number("Major direction Y.")),
            ("directionZ", Number("Major direction Z.")),
            ("normalX", Number("Normal X.")),
            ("normalY", Number("Normal Y.")),
            ("normalZ", Number("Normal Z.")),
            ("flip", Boolean("Flip path reinforcement.")),
            ("unit", String("feet, meters, or millimeters.")),
            ("parameters", Parameters)), "hostId");

        public static object UpdateRebarSystem => Object(Props(
            ("elementId", String("Reinforcement element id.")),
            ("typeId", String("Optional new type ElementId.")),
            ("parameters", Parameters)), "elementId");

        public static object CreateCoupler => Object(Props(
            ("couplerTypeId", String("Optional coupler type ElementId. Defaults to inferred coupler type.")),
            ("firstRebarId", String("First Rebar ElementId.")),
            ("firstEnd", Integer("First rebar end, 0 or 1.")),
            ("secondRebarId", String("Optional second Rebar ElementId.")),
            ("secondEnd", Integer("Second rebar end, 0 or 1.")),
            ("parameters", Parameters)), "firstRebarId");

        public static object UpdateCoupler => Object(Props(
            ("couplerId", String("RebarCoupler ElementId.")),
            ("couplerMark", String("Optional coupler mark.")),
            ("rotationAngleDegrees", Number("Optional rotation angle in degrees.")),
            ("parameters", Parameters)), "couplerId");

        public static object ChangeCouplerType => Object(Props(
            ("couplerId", String("RebarCoupler ElementId.")),
            ("couplerTypeId", String("New coupler type ElementId."))), "couplerId", "couplerTypeId");

        public static object EndTreatment => Object(Props(
            ("rebarId", String("Rebar ElementId.")),
            ("end", Integer("End index, 0 or 1.")),
            ("endTreatmentTypeId", String("EndTreatmentType ElementId."))), "rebarId", "end", "endTreatmentTypeId");

        public static object RebarTag => Object(Props(
            ("viewId", String("Optional view ElementId. Defaults to active view.")),
            ("elementId", String("ElementId to tag.")),
            ("tagTypeId", String("Optional tag type ElementId.")),
            ("x", Number("Tag X.")),
            ("y", Number("Tag Y.")),
            ("z", Number("Tag Z.")),
            ("unit", String("feet, meters, or millimeters.")),
            ("addLeader", Boolean("Create tag leader."))), "elementId", "x", "y", "z");

        public static object MultiRebarAnnotation => Object(Props(
            ("viewId", String("Optional view ElementId. Defaults to active view.")),
            ("elementIds", StringArray("Rebar ElementIds to annotate.")),
            ("typeId", String("Optional MultiReferenceAnnotationType ElementId.")),
            ("tagHeadX", Number("Tag head X.")),
            ("tagHeadY", Number("Tag head Y.")),
            ("tagHeadZ", Number("Tag head Z.")),
            ("dimensionOriginX", Number("Dimension origin X.")),
            ("dimensionOriginY", Number("Dimension origin Y.")),
            ("dimensionOriginZ", Number("Dimension origin Z.")),
            ("dimensionDirectionX", Number("Dimension line direction X.")),
            ("dimensionDirectionY", Number("Dimension line direction Y.")),
            ("dimensionDirectionZ", Number("Dimension line direction Z.")),
            ("dimensionPlaneNormalX", Number("Dimension plane normal X.")),
            ("dimensionPlaneNormalY", Number("Dimension plane normal Y.")),
            ("dimensionPlaneNormalZ", Number("Dimension plane normal Z.")),
            ("unit", String("feet, meters, or millimeters.")),
            ("addLeader", Boolean("Create tag leader."))), "elementIds");

        public static object RebarSchedule => Object(Props(
            ("name", String("Optional schedule name.")),
            ("fieldNames", StringArray("Schedulable field display names to add."))));

        public static object RebarQuantities => Object(Props(
            ("hostId", String("Optional host ElementId.")),
            ("includeCouplers", Boolean("Include coupler quantities.")),
            ("maxItems", Integer("Maximum items to inspect.")),
            ("useActiveView", UseActiveView),
            ("viewId", QueryViewId)));

        public static object SetPartition => Object(Props(
            ("elementIds", StringArray("Rebar or coupler ElementIds.")),
            ("partition", String("Partition value.")),
            ("maxItems", Integer("Maximum items allowed."))), "elementIds", "partition");

        public static object Workflow => Object(Props(
            ("hostId", String("Valid rebar host ElementId.")),
            ("barTypeId", String("Optional RebarBarType ElementId.")),
            ("hookTypeId", String("Optional RebarHookType ElementId.")),
            ("cover", Number("Cover offset.")),
            ("spacing", Number("Spacing hint.")),
            ("count", Integer("Number of generated bars/sets.")),
            ("unit", String("feet, meters, or millimeters.")),
            ("parameters", Parameters)), "hostId");
    }
}

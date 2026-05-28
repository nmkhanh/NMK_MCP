using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Models
{
    public sealed class RebarListRequest
    {
        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("kind")]
        public string? Kind { get; set; }

        [JsonProperty("includeParameters")]
        public bool IncludeParameters { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 500;
    }

    public sealed class RebarHostCandidatesRequest
    {
        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 200;
    }

    public sealed class RebarElementRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("includeParameters")]
        public bool IncludeParameters { get; set; }
    }

    public sealed class RebarTypeListRequest
    {
        [JsonProperty("nameContains")]
        public string? NameContains { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 500;
    }

    public sealed class RebarPointRequest
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }
    }

    public sealed class RebarCurveRequest
    {
        [JsonProperty("start")]
        public RebarPointRequest Start { get; set; } = new();

        [JsonProperty("end")]
        public RebarPointRequest End { get; set; } = new();
    }

    public sealed class CreateRebarFromCurvesRequest
    {
        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("barTypeId")]
        public string? BarTypeId { get; set; }

        [JsonProperty("startHookTypeId")]
        public string? StartHookTypeId { get; set; }

        [JsonProperty("endHookTypeId")]
        public string? EndHookTypeId { get; set; }

        [JsonProperty("style")]
        public string Style { get; set; } = "standard";

        [JsonProperty("startHookOrientation")]
        public string StartHookOrientation { get; set; } = "left";

        [JsonProperty("endHookOrientation")]
        public string EndHookOrientation { get; set; } = "right";

        [JsonProperty("normalX")]
        public double NormalX { get; set; } = 0;

        [JsonProperty("normalY")]
        public double NormalY { get; set; } = 0;

        [JsonProperty("normalZ")]
        public double NormalZ { get; set; } = 1;

        [JsonProperty("curves")]
        public List<RebarCurveRequest> Curves { get; set; } = new();

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("useExistingShapeIfPossible")]
        public bool UseExistingShapeIfPossible { get; set; } = true;

        [JsonProperty("createNewShape")]
        public bool CreateNewShape { get; set; } = true;

        [JsonProperty("layoutRule")]
        public string? LayoutRule { get; set; }

        [JsonProperty("count")]
        public int? Count { get; set; }

        [JsonProperty("spacing")]
        public double? Spacing { get; set; }

        [JsonProperty("arrayLength")]
        public double? ArrayLength { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateRebarFromShapeRequest
    {
        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("shapeId")]
        public string? ShapeId { get; set; }

        [JsonProperty("barTypeId")]
        public string? BarTypeId { get; set; }

        [JsonProperty("origin")]
        public RebarPointRequest Origin { get; set; } = new();

        [JsonProperty("xVector")]
        public RebarPointRequest XVector { get; set; } = new() { X = 1, Y = 0, Z = 0 };

        [JsonProperty("yVector")]
        public RebarPointRequest YVector { get; set; } = new() { X = 0, Y = 1, Z = 0 };

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class RebarLayoutRequest
    {
        [JsonProperty("rebarId")]
        public string? RebarId { get; set; }

        [JsonProperty("layoutRule")]
        public string LayoutRule { get; set; } = "single";

        [JsonProperty("count")]
        public int? Count { get; set; }

        [JsonProperty("spacing")]
        public double? Spacing { get; set; }

        [JsonProperty("arrayLength")]
        public double? ArrayLength { get; set; }

        [JsonProperty("barsOnNormalSide")]
        public bool BarsOnNormalSide { get; set; } = true;

        [JsonProperty("includeFirstBar")]
        public bool IncludeFirstBar { get; set; } = true;

        [JsonProperty("includeLastBar")]
        public bool IncludeLastBar { get; set; } = true;

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";
    }

    public sealed class RebarHooksRequest
    {
        [JsonProperty("rebarId")]
        public string? RebarId { get; set; }

        [JsonProperty("startHookTypeId")]
        public string? StartHookTypeId { get; set; }

        [JsonProperty("endHookTypeId")]
        public string? EndHookTypeId { get; set; }
    }

    public sealed class RebarConstraintsRequest
    {
        [JsonProperty("rebarId")]
        public string? RebarId { get; set; }

        [JsonProperty("useRebarConstraintsToProduceVaryingBars")]
        public bool? UseRebarConstraintsToProduceVaryingBars { get; set; }
    }

    public sealed class RebarCoverRequest
    {
        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("coverTypeId")]
        public string? CoverTypeId { get; set; }
    }

    public sealed class RebarVisibilityRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("unobscured")]
        public bool? Unobscured { get; set; }

        [JsonProperty("presentationMode")]
        public string? PresentationMode { get; set; }

        [JsonProperty("barIndex")]
        public int? BarIndex { get; set; }

        [JsonProperty("hidden")]
        public bool? Hidden { get; set; }
    }

    public sealed class RebarIdsRequest
    {
        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("dryRun")]
        public bool DryRun { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 100;
    }

    public sealed class RebarSystemRequest
    {
        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("barTypeId")]
        public string? BarTypeId { get; set; }

        [JsonProperty("hookTypeId")]
        public string? HookTypeId { get; set; }

        [JsonProperty("startHookTypeId")]
        public string? StartHookTypeId { get; set; }

        [JsonProperty("endHookTypeId")]
        public string? EndHookTypeId { get; set; }

        [JsonProperty("fabricSheetTypeId")]
        public string? FabricSheetTypeId { get; set; }

        [JsonProperty("curves")]
        public List<RebarCurveRequest> Curves { get; set; } = new();

        [JsonProperty("boundary")]
        public List<RebarPointRequest> Boundary { get; set; } = new();

        [JsonProperty("directionX")]
        public double DirectionX { get; set; } = 1;

        [JsonProperty("directionY")]
        public double DirectionY { get; set; } = 0;

        [JsonProperty("directionZ")]
        public double DirectionZ { get; set; } = 0;

        [JsonProperty("normalX")]
        public double NormalX { get; set; } = 0;

        [JsonProperty("normalY")]
        public double NormalY { get; set; } = 0;

        [JsonProperty("normalZ")]
        public double NormalZ { get; set; } = 1;

        [JsonProperty("flip")]
        public bool Flip { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class UpdateRebarSystemRequest
    {
        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class CreateRebarCouplerRequest
    {
        [JsonProperty("couplerTypeId")]
        public string? CouplerTypeId { get; set; }

        [JsonProperty("firstRebarId")]
        public string? FirstRebarId { get; set; }

        [JsonProperty("firstEnd")]
        public int FirstEnd { get; set; } = 1;

        [JsonProperty("secondRebarId")]
        public string? SecondRebarId { get; set; }

        [JsonProperty("secondEnd")]
        public int SecondEnd { get; set; } = 0;

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class UpdateRebarCouplerRequest
    {
        [JsonProperty("couplerId")]
        public string? CouplerId { get; set; }

        [JsonProperty("couplerMark")]
        public string? CouplerMark { get; set; }

        [JsonProperty("rotationAngleDegrees")]
        public double? RotationAngleDegrees { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }

    public sealed class ChangeRebarCouplerTypeRequest
    {
        [JsonProperty("couplerId")]
        public string? CouplerId { get; set; }

        [JsonProperty("couplerTypeId")]
        public string? CouplerTypeId { get; set; }
    }

    public sealed class EndTreatmentRequest
    {
        [JsonProperty("rebarId")]
        public string? RebarId { get; set; }

        [JsonProperty("end")]
        public int End { get; set; }

        [JsonProperty("endTreatmentTypeId")]
        public string? EndTreatmentTypeId { get; set; }
    }

    public sealed class RebarTagRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("elementId")]
        public string? ElementId { get; set; }

        [JsonProperty("tagTypeId")]
        public string? TagTypeId { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("addLeader")]
        public bool AddLeader { get; set; }
    }

    public sealed class MultiRebarAnnotationRequest
    {
        [JsonProperty("viewId")]
        public string? ViewId { get; set; }

        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("typeId")]
        public string? TypeId { get; set; }

        [JsonProperty("tagHeadX")]
        public double TagHeadX { get; set; }

        [JsonProperty("tagHeadY")]
        public double TagHeadY { get; set; }

        [JsonProperty("tagHeadZ")]
        public double TagHeadZ { get; set; }

        [JsonProperty("dimensionOriginX")]
        public double DimensionOriginX { get; set; }

        [JsonProperty("dimensionOriginY")]
        public double DimensionOriginY { get; set; }

        [JsonProperty("dimensionOriginZ")]
        public double DimensionOriginZ { get; set; }

        [JsonProperty("dimensionDirectionX")]
        public double DimensionDirectionX { get; set; } = 1;

        [JsonProperty("dimensionDirectionY")]
        public double DimensionDirectionY { get; set; }

        [JsonProperty("dimensionDirectionZ")]
        public double DimensionDirectionZ { get; set; }

        [JsonProperty("dimensionPlaneNormalX")]
        public double DimensionPlaneNormalX { get; set; }

        [JsonProperty("dimensionPlaneNormalY")]
        public double DimensionPlaneNormalY { get; set; }

        [JsonProperty("dimensionPlaneNormalZ")]
        public double DimensionPlaneNormalZ { get; set; } = 1;

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("addLeader")]
        public bool AddLeader { get; set; }
    }

    public sealed class RebarScheduleRequest
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("fieldNames")]
        public List<string> FieldNames { get; set; } = new();
    }

    public sealed class RebarQuantityRequest
    {
        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("includeCouplers")]
        public bool IncludeCouplers { get; set; } = true;

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 1000;
    }

    public sealed class SetRebarPartitionRequest
    {
        [JsonProperty("elementIds")]
        public List<string> ElementIds { get; set; } = new();

        [JsonProperty("partition")]
        public string? Partition { get; set; }

        [JsonProperty("maxItems")]
        public int MaxItems { get; set; } = 200;
    }

    public sealed class RebarWorkflowRequest
    {
        [JsonProperty("hostId")]
        public string? HostId { get; set; }

        [JsonProperty("barTypeId")]
        public string? BarTypeId { get; set; }

        [JsonProperty("hookTypeId")]
        public string? HookTypeId { get; set; }

        [JsonProperty("cover")]
        public double Cover { get; set; } = 0.15;

        [JsonProperty("spacing")]
        public double Spacing { get; set; } = 1.0;

        [JsonProperty("count")]
        public int Count { get; set; } = 4;

        [JsonProperty("unit")]
        public string Unit { get; set; } = "feet";

        [JsonProperty("parameters")]
        public Dictionary<string, JToken?> Parameters { get; set; } = new();
    }
}

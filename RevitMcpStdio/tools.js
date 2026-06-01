'use strict';

// ============================================================
//  tools.js
//  Hardcoded MCP tool definitions for the three Revit tools.
//  These are used as fallback when Revit is offline, and are
//  also replaced at runtime with live definitions fetched from
//  the Revit add-in once the SSE connection is established.
//
//  Note: MCP uses "inputSchema" (not "input_schema") in JSON-
//        RPC 2.0 mode.  Claude Desktop accepts both forms; we
//        emit both so the server works regardless of client.
// ============================================================

const stringProp = (description) => ({ type: 'string', description });
const numberProp = (description) => ({ type: 'number', description });
const integerProp = (description) => ({ type: 'integer', description });
const booleanProp = (description) => ({ type: 'boolean', description });
const stringArrayProp = (description) => ({ type: 'array', description, items: { type: 'string' } });
const pointProp = {
  type: 'object',
  properties: {
    x: numberProp('X coordinate.'),
    y: numberProp('Y coordinate.'),
    z: numberProp('Z coordinate.')
  },
  required: ['x', 'y', 'z']
};
const curveProp = {
  type: 'object',
  properties: { start: pointProp, end: pointProp },
  required: ['start', 'end']
};
const curvesProp = { type: 'array', description: 'Line curves as start/end point pairs.', items: curveProp };
const paramsProp = { type: 'object', description: 'Optional instance parameter values keyed by parameter name.', additionalProperties: true };
const toolSchema = (properties = {}, required = []) => ({ type: 'object', properties, required });
const rebarTool = (name, description, properties = {}, required = []) => ({
  name,
  description,
  inputSchema: toolSchema(properties, required)
});

const rebarListProps = {
  hostId: stringProp('Optional host ElementId.'),
  kind: stringProp('Optional reinforcement kind hint.'),
  includeParameters: booleanProp('Include instance parameters.'),
  maxItems: integerProp('Maximum items to return.')
};
const typeListProps = {
  nameContains: stringProp('Optional case-insensitive name filter.'),
  maxItems: integerProp('Maximum items to return.')
};
const elementIdProps = {
  elementId: stringProp('Revit ElementId.'),
  includeParameters: booleanProp('Include instance parameters.')
};
const elementIdsProps = {
  elementIds: stringArrayProp('ElementIds to process.'),
  dryRun: booleanProp('Validate without modifying the document.'),
  maxItems: integerProp('Maximum items allowed.')
};
const activeViewScopeProps = {
  useActiveView: { type: 'boolean', description: 'When true, scope the query to elements visible in the active view. Default: false.', default: false },
  viewId: stringProp('Optional view ElementId to scope the query. Overrides useActiveView when supplied.')
};
const rebarSystemProps = {
  hostId: stringProp('Valid rebar host ElementId.'),
  typeId: stringProp('Area/path/fabric type ElementId.'),
  barTypeId: stringProp('RebarBarType or fabric sheet type ElementId depending on tool.'),
  hookTypeId: stringProp('Optional RebarHookType ElementId.'),
  startHookTypeId: stringProp('Optional start RebarHookType ElementId.'),
  endHookTypeId: stringProp('Optional end RebarHookType ElementId.'),
  fabricSheetTypeId: stringProp('Optional FabricSheetType ElementId.'),
  curves: curvesProp,
  boundary: { type: 'array', description: 'Boundary points.', items: pointProp },
  directionX: numberProp('Major direction X.'),
  directionY: numberProp('Major direction Y.'),
  directionZ: numberProp('Major direction Z.'),
  normalX: numberProp('Normal X.'),
  normalY: numberProp('Normal Y.'),
  normalZ: numberProp('Normal Z.'),
  flip: booleanProp('Flip path reinforcement.'),
  unit: stringProp('feet, meters, or millimeters.'),
  parameters: paramsProp
};
const workflowProps = {
  hostId: stringProp('Valid rebar host ElementId.'),
  barTypeId: stringProp('Optional RebarBarType ElementId.'),
  hookTypeId: stringProp('Optional RebarHookType ElementId.'),
  cover: numberProp('Cover offset.'),
  spacing: numberProp('Spacing hint.'),
  count: integerProp('Number of generated bars/sets.'),
  unit: stringProp('feet, meters, or millimeters.'),
  parameters: paramsProp
};
const createCouplerProps = {
  couplerTypeId: stringProp('Optional coupler type ElementId. Defaults to inferred coupler type.'),
  firstRebarId: stringProp('First Rebar ElementId.'),
  firstEnd: integerProp('First rebar end, 0 or 1.'),
  secondRebarId: stringProp('Optional second Rebar ElementId.'),
  secondEnd: integerProp('Second rebar end, 0 or 1.'),
  parameters: paramsProp
};
const REBAR_TOOLS = [
  rebarTool('get_rebars', 'Lists Rebar elements with optional host filtering.', { ...rebarListProps, ...activeViewScopeProps }),
  rebarTool('get_rebar_host_candidates', 'Lists concrete/structural elements that can host reinforcement.', {
    category: stringProp('Optional BuiltInCategory suffix, e.g. StructuralColumns.'),
    maxItems: integerProp('Maximum items to return.'),
    ...activeViewScopeProps
  }),
  rebarTool('get_rebar_bar_types', 'Lists available RebarBarType elements.', typeListProps),
  rebarTool('get_rebar_shapes', 'Lists available RebarShape elements.', typeListProps),
  rebarTool('get_rebar_hook_types', 'Lists available RebarHookType elements.', typeListProps),
  rebarTool('get_rebar_cover_types', 'Lists rebar cover types and distances.', typeListProps),
  rebarTool('get_rebar_constraints', 'Reads a rebar constraint/accessor summary.', elementIdProps, ['elementId']),
  rebarTool('get_rebar_centerline_curves', 'Reads centerline curves for a rebar.', elementIdProps, ['elementId']),
  rebarTool('create_rebar_from_curves', 'Creates shape-driven rebar from line curves on a valid host.', {
    hostId: stringProp('Valid rebar host ElementId.'),
    barTypeId: stringProp('Optional RebarBarType ElementId. Defaults to first available type.'),
    startHookTypeId: stringProp('Optional start RebarHookType ElementId.'),
    endHookTypeId: stringProp('Optional end RebarHookType ElementId.'),
    style: stringProp('standard or stirrup_tie.'),
    startHookOrientation: stringProp('left or right.'),
    endHookOrientation: stringProp('left or right.'),
    normalX: numberProp('Rebar plane normal X.'),
    normalY: numberProp('Rebar plane normal Y.'),
    normalZ: numberProp('Rebar plane normal Z.'),
    curves: curvesProp,
    unit: stringProp('feet, meters, or millimeters.'),
    useExistingShapeIfPossible: booleanProp('Reuse a matching RebarShape when possible.'),
    createNewShape: booleanProp('Create a new RebarShape if needed.'),
    layoutRule: stringProp('Optional layout rule.'),
    count: integerProp('Optional bar count.'),
    spacing: numberProp('Optional spacing.'),
    arrayLength: numberProp('Optional array length.'),
    parameters: paramsProp
  }, ['hostId', 'curves']),
  rebarTool('create_rebar_from_shape', 'Creates shape-driven rebar from an existing RebarShape.', {
    hostId: stringProp('Valid rebar host ElementId.'),
    shapeId: stringProp('RebarShape ElementId.'),
    barTypeId: stringProp('Optional RebarBarType ElementId.'),
    origin: pointProp,
    xVector: pointProp,
    yVector: pointProp,
    unit: stringProp('feet, meters, or millimeters.'),
    parameters: paramsProp
  }, ['hostId', 'shapeId']),
  rebarTool('update_rebar_layout', 'Updates shape-driven rebar layout rule, count, spacing, and array length.', {
    rebarId: stringProp('Rebar ElementId.'),
    layoutRule: stringProp('single, number_with_spacing, fixed_number, maximum_spacing, or minimum_clear_spacing.'),
    count: integerProp('Optional bar count.'),
    spacing: numberProp('Optional spacing.'),
    arrayLength: numberProp('Optional array length.'),
    barsOnNormalSide: booleanProp('Bars on normal side.'),
    includeFirstBar: booleanProp('Include first bar.'),
    includeLastBar: booleanProp('Include last bar.'),
    unit: stringProp('feet, meters, or millimeters.')
  }, ['rebarId', 'layoutRule']),
  rebarTool('update_rebar_hooks', 'Updates start/end hook type ids for a rebar.', {
    rebarId: stringProp('Rebar ElementId.'),
    startHookTypeId: stringProp('Optional start hook type ElementId.'),
    endHookTypeId: stringProp('Optional end hook type ElementId.')
  }, ['rebarId']),
  rebarTool('update_rebar_constraints', 'Updates supported rebar constraint options and recomputes when available.', {
    rebarId: stringProp('Rebar ElementId.'),
    useRebarConstraintsToProduceVaryingBars: booleanProp('Toggle varying bars driven by constraints.')
  }, ['rebarId']),
  rebarTool('set_rebar_cover', 'Sets the common rebar cover type on a host.', {
    hostId: stringProp('Valid rebar host ElementId.'),
    coverTypeId: stringProp('Optional RebarCoverType ElementId. Defaults to first available type.')
  }, ['hostId']),
  rebarTool('set_rebar_visibility_in_view', 'Controls rebar/coupler visibility in a Revit view.', {
    elementId: stringProp('Rebar, AreaReinforcement, or RebarCoupler ElementId.'),
    viewId: stringProp('Optional view ElementId. Defaults to active view.'),
    unobscured: booleanProp('Show unobscured in view.'),
    presentationMode: stringProp('Optional RebarPresentationMode.'),
    barIndex: integerProp('Optional bar index for hidden status.'),
    hidden: booleanProp('Bar hidden status.')
  }, ['elementId']),
  rebarTool('delete_rebars', 'Deletes Rebar elements by id, with dry-run support.', elementIdsProps, ['elementIds']),
  rebarTool('create_area_reinforcement', 'Creates AreaReinforcement from curves, boundary points, or host bounds.', rebarSystemProps, ['hostId']),
  rebarTool('update_area_reinforcement', 'Updates type/parameters on an AreaReinforcement element.', {
    elementId: stringProp('Reinforcement element id.'),
    typeId: stringProp('Optional new type ElementId.'),
    parameters: paramsProp
  }, ['elementId']),
  rebarTool('create_path_reinforcement', 'Creates PathReinforcement from curves or host bounds.', rebarSystemProps, ['hostId']),
  rebarTool('update_path_reinforcement', 'Updates type/parameters on a PathReinforcement element.', {
    elementId: stringProp('Reinforcement element id.'),
    typeId: stringProp('Optional new type ElementId.'),
    parameters: paramsProp
  }, ['elementId']),
  rebarTool('create_fabric_area', 'Creates FabricArea from host bounds or boundary loops.', rebarSystemProps, ['hostId']),
  rebarTool('update_fabric_area', 'Updates type/parameters on a FabricArea element.', {
    elementId: stringProp('Reinforcement element id.'),
    typeId: stringProp('Optional new type ElementId.'),
    parameters: paramsProp
  }, ['elementId']),
  rebarTool('place_fabric_sheet', 'Places a FabricSheet on a valid host.', rebarSystemProps, ['hostId']),
  rebarTool('update_fabric_sheet', 'Updates type/parameters on a FabricSheet element.', {
    elementId: stringProp('Reinforcement element id.'),
    typeId: stringProp('Optional new type ElementId.'),
    parameters: paramsProp
  }, ['elementId']),
  rebarTool('get_rebar_coupler_types', 'Lists rebar coupler type candidates.', typeListProps),
  rebarTool('get_rebar_couplers', 'Lists RebarCoupler elements.', { ...rebarListProps, ...activeViewScopeProps }),
  rebarTool('get_rebar_coupler', 'Reads one RebarCoupler by id.', elementIdProps, ['elementId']),
  rebarTool('create_rebar_coupler', 'Creates a RebarCoupler on one or two rebar ends.', createCouplerProps, ['firstRebarId']),
  rebarTool('update_rebar_coupler', 'Updates mark, rotation, and parameters on a RebarCoupler.', {
    couplerId: stringProp('RebarCoupler ElementId.'),
    couplerMark: stringProp('Optional coupler mark.'),
    rotationAngleDegrees: numberProp('Optional rotation angle in degrees.'),
    parameters: paramsProp
  }, ['couplerId']),
  rebarTool('change_rebar_coupler_type', 'Changes the type of a RebarCoupler.', {
    couplerId: stringProp('RebarCoupler ElementId.'),
    couplerTypeId: stringProp('New coupler type ElementId.')
  }, ['couplerId', 'couplerTypeId']),
  rebarTool('delete_rebar_couplers', 'Deletes RebarCoupler elements by id, with dry-run support.', elementIdsProps, ['elementIds']),
  rebarTool('get_rebar_end_treatments', 'Lists EndTreatmentType elements.', typeListProps),
  rebarTool('set_rebar_end_treatment', 'Sets an end treatment type on one rebar end.', {
    rebarId: stringProp('Rebar ElementId.'),
    end: integerProp('End index, 0 or 1.'),
    endTreatmentTypeId: stringProp('EndTreatmentType ElementId.')
  }, ['rebarId', 'end', 'endTreatmentTypeId']),
  rebarTool('validate_rebar_coupler_placement', 'Validates rebar coupler placement inputs without creating a coupler.', createCouplerProps, ['firstRebarId']),
  rebarTool('create_rebar_tag', 'Creates an IndependentTag for a rebar/reinforcement element.', {
    viewId: stringProp('Optional view ElementId. Defaults to active view.'),
    elementId: stringProp('ElementId to tag.'),
    tagTypeId: stringProp('Optional tag type ElementId.'),
    x: numberProp('Tag X.'),
    y: numberProp('Tag Y.'),
    z: numberProp('Tag Z.'),
    unit: stringProp('feet, meters, or millimeters.'),
    addLeader: booleanProp('Create tag leader.')
  }, ['elementId', 'x', 'y', 'z']),
  rebarTool('create_multi_rebar_annotation', 'Creates a MultiReferenceAnnotation for one or more rebars.', {
    viewId: stringProp('Optional view ElementId. Defaults to active view.'),
    elementIds: stringArrayProp('Rebar ElementIds to annotate.'),
    typeId: stringProp('Optional MultiReferenceAnnotationType ElementId.'),
    tagHeadX: numberProp('Tag head X.'),
    tagHeadY: numberProp('Tag head Y.'),
    tagHeadZ: numberProp('Tag head Z.'),
    dimensionOriginX: numberProp('Dimension origin X.'),
    dimensionOriginY: numberProp('Dimension origin Y.'),
    dimensionOriginZ: numberProp('Dimension origin Z.'),
    dimensionDirectionX: numberProp('Dimension line direction X.'),
    dimensionDirectionY: numberProp('Dimension line direction Y.'),
    dimensionDirectionZ: numberProp('Dimension line direction Z.'),
    dimensionPlaneNormalX: numberProp('Dimension plane normal X.'),
    dimensionPlaneNormalY: numberProp('Dimension plane normal Y.'),
    dimensionPlaneNormalZ: numberProp('Dimension plane normal Z.'),
    unit: stringProp('feet, meters, or millimeters.'),
    addLeader: booleanProp('Create tag leader.')
  }, ['elementIds']),
  rebarTool('create_rebar_schedule', 'Creates a Rebar schedule with optional fields.', {
    name: stringProp('Optional schedule name.'),
    fieldNames: stringArrayProp('Schedulable field display names to add.')
  }),
  rebarTool('get_rebar_quantities', 'Returns bar and coupler quantity summaries.', {
    hostId: stringProp('Optional host ElementId.'),
    includeCouplers: booleanProp('Include coupler quantities.'),
    maxItems: integerProp('Maximum items to inspect.'),
    ...activeViewScopeProps
  }),
  rebarTool('set_rebar_partition', 'Sets the Partition parameter on rebar/coupler elements.', {
    elementIds: stringArrayProp('Rebar or coupler ElementIds.'),
    partition: stringProp('Partition value.'),
    maxItems: integerProp('Maximum items allowed.')
  }, ['elementIds', 'partition']),
  rebarTool('create_column_vertical_rebars', 'Creates vertical column rebars from the host bounding box.', workflowProps, ['hostId']),
  rebarTool('create_column_ties', 'Creates column tie/stirrup rebars from the host bounding box.', workflowProps, ['hostId']),
  rebarTool('create_beam_longitudinal_rebars', 'Creates longitudinal beam rebars from the host bounding box.', workflowProps, ['hostId']),
  rebarTool('create_beam_stirrups', 'Creates beam stirrups from the host bounding box.', workflowProps, ['hostId']),
  rebarTool('create_wall_rebar_grid', 'Creates a wall rebar grid from the host bounding box.', workflowProps, ['hostId']),
  rebarTool('create_slab_rebar_grid', 'Creates a slab rebar grid from the host bounding box.', workflowProps, ['hostId'])
];

const viewOverrideProps = {
  projectionLineColor: stringProp('Projection line color as #RRGGBB.'),
  projectionLinePatternId: stringProp('Projection line pattern ElementId.'),
  projectionLineWeight: integerProp('Projection line weight.'),
  cutLineColor: stringProp('Cut line color as #RRGGBB.'),
  cutLinePatternId: stringProp('Cut line pattern ElementId.'),
  cutLineWeight: integerProp('Cut line weight.'),
  surfaceForegroundPatternId: stringProp('Surface foreground fill pattern ElementId.'),
  surfaceForegroundPatternColor: stringProp('Surface foreground fill color as #RRGGBB.'),
  surfaceForegroundPatternVisible: booleanProp('Surface foreground pattern visibility.'),
  surfaceBackgroundPatternId: stringProp('Surface background fill pattern ElementId.'),
  surfaceBackgroundPatternColor: stringProp('Surface background fill color as #RRGGBB.'),
  surfaceBackgroundPatternVisible: booleanProp('Surface background pattern visibility.'),
  cutForegroundPatternId: stringProp('Cut foreground fill pattern ElementId.'),
  cutForegroundPatternColor: stringProp('Cut foreground fill color as #RRGGBB.'),
  cutForegroundPatternVisible: booleanProp('Cut foreground pattern visibility.'),
  cutBackgroundPatternId: stringProp('Cut background fill pattern ElementId.'),
  cutBackgroundPatternColor: stringProp('Cut background fill color as #RRGGBB.'),
  cutBackgroundPatternVisible: booleanProp('Cut background pattern visibility.'),
  transparency: integerProp('Surface transparency from 0 to 100.'),
  halftone: booleanProp('Halftone override.'),
  detailLevel: stringProp('coarse, medium, fine, or undefined.')
};
const overridesProp = { type: 'object', description: 'Graphic override settings.', properties: viewOverrideProps };
const viewTargetProps = { viewId: stringProp('Optional view ElementId. Defaults to active view.') };
const categoryTargetProps = {
  ...viewTargetProps,
  categoryId: stringProp('Category ElementId.'),
  category: stringProp('BuiltInCategory name or suffix, e.g. Walls or OST_Walls.')
};
const filterTargetProps = {
  ...viewTargetProps,
  filterId: stringProp('ParameterFilterElement or selection filter ElementId.')
};
const VIEW_OVERRIDE_TOOLS = [
  rebarTool('get_view_overrides', 'Reads element, category, and filter override graphics in a view.', {
    ...viewTargetProps,
    elementIds: stringArrayProp('ElementIds whose overrides should be read.'),
    categoryIds: stringArrayProp('Category ElementIds whose overrides should be read.'),
    categories: stringArrayProp('BuiltInCategory names or suffixes whose overrides should be read.'),
    filterIds: stringArrayProp('Filter ElementIds to inspect. Defaults to filters already on the view.'),
    includeDefaults: booleanProp('Include unset/default override properties.')
  }),
  rebarTool('set_element_overrides_in_view', 'Applies graphic overrides to elements in a view.', {
    ...viewTargetProps,
    elementIds: stringArrayProp('ElementIds to override.'),
    overrides: overridesProp,
    dryRun: booleanProp('Validate targets without changing the model.'),
    maxItems: integerProp('Maximum element count allowed.')
  }, ['elementIds', 'overrides']),
  rebarTool('clear_element_overrides_in_view', 'Clears element graphic overrides in a view.', {
    ...viewTargetProps,
    elementIds: stringArrayProp('ElementIds whose element overrides should be cleared.'),
    dryRun: booleanProp('Validate targets without changing the model.'),
    maxItems: integerProp('Maximum element count allowed.')
  }, ['elementIds']),
  rebarTool('set_category_overrides_in_view', 'Applies graphic overrides to one category in a view.', {
    ...categoryTargetProps,
    overrides: overridesProp
  }, ['overrides']),
  rebarTool('clear_category_overrides_in_view', 'Clears category graphic overrides in a view.', categoryTargetProps),
  rebarTool('set_category_visibility_in_view', 'Shows or hides one category in a view.', {
    ...categoryTargetProps,
    visible: booleanProp('True to show the category, false to hide it.')
  }, ['visible']),
  rebarTool('set_filter_overrides_in_view', 'Applies graphic overrides and visibility settings to a view filter.', {
    ...filterTargetProps,
    overrides: overridesProp,
    visible: booleanProp('Optional filter visibility in the view.'),
    enabled: booleanProp('Optional filter enabled state when supported by this Revit API.')
  }, ['filterId', 'overrides']),
  rebarTool('clear_filter_overrides_in_view', 'Clears graphic overrides for a view filter.', filterTargetProps, ['filterId']),
  rebarTool('add_filter_to_view', 'Adds a filter to a view and optionally applies visibility or overrides.', {
    ...filterTargetProps,
    overrides: overridesProp,
    visible: booleanProp('Optional filter visibility in the view.'),
    enabled: booleanProp('Optional filter enabled state when supported by this Revit API.')
  }, ['filterId']),
  rebarTool('remove_filter_from_view', 'Removes a filter from a view.', filterTargetProps, ['filterId']),
  rebarTool('set_view_detail_graphics', 'Updates detail level, display style, parts visibility, and discipline for a view.', {
    ...viewTargetProps,
    detailLevel: stringProp('coarse, medium, or fine.'),
    displayStyle: stringProp('wireframe, hidden_line, shaded, consistent_colors, realistic, or flat_colors.'),
    partsVisibility: stringProp('show_parts_only, show_original_only, or show_parts_and_original.'),
    discipline: stringProp('architecture, structural, mechanical, electrical, coordination, or plumbing.')
  }),
  rebarTool('create_view_graphics_override_preset', 'Stores a named graphic override preset for this add-in session.', {
    name: stringProp('Preset name for this add-in session.'),
    overrides: overridesProp
  }, ['name', 'overrides']),
  rebarTool('apply_view_graphics_override_preset', 'Applies a named graphic override preset to elements, categories, or filters in a view.', {
    ...viewTargetProps,
    targetType: stringProp('element, category, or filter.'),
    targetIds: stringArrayProp('ElementIds, category ids, or filter ids to receive the preset.'),
    presetName: stringProp('Preset name created earlier.'),
    visible: booleanProp('Optional visibility for category/filter targets.'),
    enabled: booleanProp('Optional enabled state for filter targets.'),
    maxItems: integerProp('Maximum target count allowed.')
  }, ['targetType', 'targetIds', 'presetName'])
];

/** @type {import('./types').McpTool[]} */
const TOOLS = [
  ...REBAR_TOOLS,
  ...VIEW_OVERRIDE_TOOLS,
  // ── select_elements_by_ids ───────────────────────────────
  {
    name:        'select_elements_by_ids',
    description: 'Select and highlight elements in the Revit UI by ElementId. ' +
                 'Optionally zooms the active view to fit the selection.',
    inputSchema: {
      type:       'object',
      properties: {
        ids: {
          type:        'array',
          description: 'List of integer ElementIds to select (e.g. [687839, 665246]).',
          items:       { type: 'integer' }
        },
        zoom: {
          type:        'boolean',
          description: 'Zoom the active view to fit the selected elements. Default: true.',
          default:     true
        },
        clearPrevious: {
          type:        'boolean',
          description: 'Clear the existing selection before selecting new elements. Default: true.',
          default:     true
        }
      },
      required: ['ids']
    }
  },

  // ── get_active_document ──────────────────────────────────
  {
    name:        'get_active_document',
    description: 'Returns metadata about the currently active Revit document: ' +
                 'title, file path, modification state, worksharing, active view, ' +
                 'element count, and Revit version.',
    inputSchema: {
      type:       'object',
      properties: {},
      required:   []
    }
  },

  // ── get_elements ─────────────────────────────────────────
  {
    name:        'get_elements',
    description: 'Returns elements from the active Revit document filtered by ' +
                 'category and optional parameter values. ' +
                 'Use the BuiltInCategory suffix as the category name: ' +
                 '"Walls", "Doors", "Windows", "Floors", "Columns", "Stairs", "Rooms", "Sheets".',
    inputSchema: {
      type:       'object',
      properties: {
        category: {
          type:        'string',
          description: 'Revit BuiltInCategory suffix, e.g. "Walls", "Doors", "Floors", "Sheets".'
        },
        includeParameters: {
          type:        'boolean',
          description: 'Include Revit element parameters in the response. Slow on large result sets. Default: false.',
          default:     false
        },
        ...activeViewScopeProps,
        parameterFilters: {
          type:        'array',
          description: 'Optional filters on parameter values (AND logic). ' +
                       'Works with built-in parameters (e.g. "Comments", "Mark") ' +
                       'and shared parameters by display name. ' +
                       'Checks both instance and type parameters. Case-insensitive exact match.',
          items: {
            type:       'object',
            properties: {
              name:  { type: 'string', description: 'Parameter display name.' },
              value: { type: 'string', description: 'Expected value (case-insensitive).' }
            },
            required: ['name', 'value']
          }
        }
      },
      required: ['category']
    }
  },

  // --- get_project_info ---------------------------------------------------
  {
    name:        'get_project_info',
    description: 'Returns Revit project information, document metadata, and active view summary.',
    inputSchema: { type: 'object', properties: {}, required: [] }
  },

  // --- list_categories ----------------------------------------------------
  {
    name:        'list_categories',
    description: 'Lists Revit categories, optionally filtered by categoryType.',
    inputSchema: {
      type:       'object',
      properties: {
        categoryType: { type: 'string', description: 'Optional: model, annotation, analytical, or internal.' }
      },
      required: []
    }
  },

  // --- list_element_types -------------------------------------------------
  {
    name:        'list_element_types',
    description: 'Lists Revit ElementType records, optionally filtered by category and family name.',
    inputSchema: {
      type:       'object',
      properties: {
        category: { type: 'string', description: 'Optional BuiltInCategory suffix, e.g. Walls, Doors, Windows.' },
        familyName: { type: 'string', description: 'Optional family name exact match.' },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 500, hard max: 1000.', default: 500 }
      },
      required: []
    }
  },

  // --- list_family_types --------------------------------------------------
  {
    name:        'list_family_types',
    description: 'Lists Revit FamilySymbol ids for placement tools, optionally filtered by category and family name.',
    inputSchema: {
      type:       'object',
      properties: {
        category: { type: 'string', description: 'Optional BuiltInCategory suffix, e.g. Doors, Windows, Furniture.' },
        familyName: { type: 'string', description: 'Optional family name exact match.' },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 500, hard max: 1000.', default: 500 }
      },
      required: []
    }
  },

  // --- get_family_symbols -------------------------------------------------
  {
    name:        'get_family_symbols',
    description: 'Lists Revit FamilySymbol ids, optionally filtered by category and family name.',
    inputSchema: {
      type:       'object',
      properties: {
        category: { type: 'string', description: 'Optional BuiltInCategory suffix, e.g. Doors, Windows, Furniture.' },
        familyName: { type: 'string', description: 'Optional family name exact match.' },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 500, hard max: 1000.', default: 500 }
      },
      required: []
    }
  },

  // --- activate_family_symbol --------------------------------------------
  {
    name:        'activate_family_symbol',
    description: 'Activates a Revit FamilySymbol so it can be placed.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'FamilySymbol ElementId.' }
      },
      required: ['symbolId']
    }
  },

  // --- get_revit_links ----------------------------------------------------
  {
    name:        'get_revit_links',
    description: 'Lists Revit link instances and loaded linked document info.',
    inputSchema: { type: 'object', properties: {}, required: [] }
  },

  // --- get_design_options -------------------------------------------------
  {
    name:        'get_design_options',
    description: 'Lists design options in the active Revit document.',
    inputSchema: { type: 'object', properties: {}, required: [] }
  },

  // --- create_schedule ----------------------------------------------------
  {
    name:        'create_schedule',
    description: 'Creates a Revit schedule for a category and optional fields.',
    inputSchema: {
      type:       'object',
      properties: {
        category: { type: 'string', description: 'BuiltInCategory suffix, e.g. Walls, Doors, Rooms.' },
        categoryId: { type: 'string', description: 'Optional category ElementId alternative.' },
        name: { type: 'string', description: 'Optional schedule name.' },
        fieldNames: { type: 'array', description: 'Schedulable field names to add.', items: { type: 'string' } }
      },
      required: []
    }
  },

  // --- update_schedule ----------------------------------------------------
  {
    name:        'update_schedule',
    description: 'Updates a schedule name and/or adds fields.',
    inputSchema: {
      type:       'object',
      properties: {
        scheduleId: { type: 'string', description: 'Schedule ElementId.' },
        name: { type: 'string', description: 'Optional schedule name.' },
        fieldNames: { type: 'array', description: 'Schedulable field names to add.', items: { type: 'string' } }
      },
      required: ['scheduleId']
    }
  },

  // --- get_schedule_data --------------------------------------------------
  {
    name:        'get_schedule_data',
    description: 'Reads visible body cells from a Revit schedule.',
    inputSchema: {
      type:       'object',
      properties: {
        scheduleId: { type: 'string', description: 'Schedule ElementId.' },
        maxRows: { type: 'integer', description: 'Safety cap. Default: 500, hard max: 5000.', default: 500 },
        maxColumns: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['scheduleId']
    }
  },

  // --- export_schedule ----------------------------------------------------
  {
    name:        'export_schedule',
    description: 'Exports a Revit schedule to a text file.',
    inputSchema: {
      type:       'object',
      properties: {
        scheduleId: { type: 'string', description: 'Schedule ElementId.' },
        outputFolder: { type: 'string', description: 'Absolute output folder. Defaults to document folder or Desktop.' },
        fileName: { type: 'string', description: 'Optional output file name. Defaults to schedule name plus .txt.' }
      },
      required: ['scheduleId']
    }
  },

  // --- load_family --------------------------------------------------------
  {
    name:        'load_family',
    description: 'Loads a Revit family file into the active document.',
    inputSchema: {
      type:       'object',
      properties: {
        path: { type: 'string', description: 'Absolute .rfa file path.' },
        overwriteExisting: { type: 'boolean', description: 'Overwrite existing family when found. Default: true.', default: true },
        overwriteParameterValues: { type: 'boolean', description: 'Overwrite parameter values when reloading. Default: true.', default: true }
      },
      required: ['path']
    }
  },

  // --- reload_family ------------------------------------------------------
  {
    name:        'reload_family',
    description: 'Reloads a Revit family file and overwrites existing definitions.',
    inputSchema: {
      type:       'object',
      properties: {
        path: { type: 'string', description: 'Absolute .rfa file path.' },
        overwriteExisting: { type: 'boolean', description: 'Overwrite existing family when found. Default: true.', default: true },
        overwriteParameterValues: { type: 'boolean', description: 'Overwrite parameter values when reloading. Default: true.', default: true }
      },
      required: ['path']
    }
  },

  // --- create_group -------------------------------------------------------
  {
    name:        'create_group',
    description: 'Creates a Revit model group from element ids.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'ElementIds to group.', items: { type: 'string' } },
        name: { type: 'string', description: 'Optional group type name.' }
      },
      required: ['elementIds']
    }
  },

  // --- update_group -------------------------------------------------------
  {
    name:        'update_group',
    description: 'Updates a Revit group name and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        groupId: { type: 'string', description: 'Group ElementId.' },
        name: { type: 'string', description: 'Optional group type name.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['groupId']
    }
  },

  // --- create_assembly ----------------------------------------------------
  {
    name:        'create_assembly',
    description: 'Creates a Revit assembly from element ids.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'ElementIds to assemble.', items: { type: 'string' } },
        namingCategoryId: { type: 'string', description: 'Optional naming category id. Defaults to first member category.' },
        name: { type: 'string', description: 'Optional assembly type name.' }
      },
      required: ['elementIds']
    }
  },

  // --- update_assembly ----------------------------------------------------
  {
    name:        'update_assembly',
    description: 'Updates a Revit assembly name, members, and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        assemblyId: { type: 'string', description: 'Assembly ElementId.' },
        name: { type: 'string', description: 'Optional assembly type name.' },
        addElementIds: { type: 'array', description: 'ElementIds to add.', items: { type: 'string' } },
        removeElementIds: { type: 'array', description: 'ElementIds to remove.', items: { type: 'string' } },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['assemblyId']
    }
  },

  // --- reload_revit_link --------------------------------------------------
  {
    name:        'reload_revit_link',
    description: 'Reloads a Revit link type by type id or link instance id.',
    inputSchema: {
      type:       'object',
      properties: {
        linkTypeId: { type: 'string', description: 'RevitLinkType ElementId.' },
        linkInstanceId: { type: 'string', description: 'RevitLinkInstance ElementId alternative.' }
      },
      required: []
    }
  },

  // --- manage_worksets ----------------------------------------------------
  {
    name:        'manage_worksets',
    description: 'Lists, creates, or renames user worksets.',
    inputSchema: {
      type:       'object',
      properties: {
        action: { type: 'string', description: 'list, create, or rename. Default: list.', default: 'list' },
        worksetId: { type: 'string', description: 'Workset id for rename.' },
        name: { type: 'string', description: 'Workset name for create/rename.' }
      },
      required: []
    }
  },

  // --- set_element_design_option -----------------------------------------
  {
    name:        'set_element_design_option',
    description: 'Assigns an element to a design option when the parameter is writable.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Target ElementId.' },
        designOptionId: { type: 'string', description: 'Target DesignOption ElementId.' }
      },
      required: ['elementId', 'designOptionId']
    }
  },

  // --- get_selected_elements ---------------------------------------------
  {
    name:        'get_selected_elements',
    description: 'Returns the currently selected elements in the Revit UI.',
    inputSchema: { type: 'object', properties: {}, required: [] }
  },

  // --- find_elements_by_parameter ----------------------------------------
  {
    name:        'find_elements_by_parameter',
    description: 'Finds elements by an instance or type parameter value, optionally filtered by category.',
    inputSchema: {
      type:       'object',
      properties: {
        parameterName: { type: 'string', description: 'Parameter display name.' },
        value: { type: 'string', description: 'Optional value to match. Omit to find elements that have the parameter.' },
        category: { type: 'string', description: 'Optional BuiltInCategory suffix, e.g. Walls, Doors, Rooms.' },
        comparison: { type: 'string', description: 'equals, contains, startsWith, endsWith, or notEquals. Default: equals.', default: 'equals' },
        includeParameters: { type: 'boolean', description: 'Include a small parameter sample in results. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 200, hard max: 2000.', default: 200 },
        ...activeViewScopeProps
      },
      required: ['parameterName']
    }
  },

  // --- get_levels ----------------------------------------------------------
  {
    name:        'get_levels',
    description: 'Returns all Revit levels in the active document with id, name, and elevation.',
    inputSchema: {
      type:       'object',
      properties: { ...activeViewScopeProps },
      required:   []
    }
  },

  // --- create_level --------------------------------------------------------
  {
    name:        'create_level',
    description: 'Creates a Revit level at the requested elevation. Unit defaults to feet.',
    inputSchema: {
      type:       'object',
      properties: {
        name: {
          type:        'string',
          description: 'Optional level name. If omitted, Revit assigns a default name.'
        },
        elevation: {
          type:        'number',
          description: 'Level elevation. Interpreted using the unit field.'
        },
        unit: {
          type:        'string',
          description: 'Elevation unit: feet, meters, or millimeters. Default: feet.',
          default:     'feet'
        }
      },
      required: ['elevation']
    }
  },

  // --- update_level --------------------------------------------------------
  {
    name:        'update_level',
    description: 'Updates a Revit level name and/or elevation.',
    inputSchema: {
      type:       'object',
      properties: {
        levelId: {
          type:        'string',
          description: 'Revit ElementId of the level to update.'
        },
        name: {
          type:        'string',
          description: 'New level name.'
        },
        elevation: {
          type:        'number',
          description: 'New level elevation. Interpreted using the unit field.'
        },
        unit: {
          type:        'string',
          description: 'Elevation unit: feet, meters, or millimeters. Default: feet.',
          default:     'feet'
        }
      },
      required: ['levelId']
    }
  },

  // --- get_grids -----------------------------------------------------------
  {
    name:        'get_grids',
    description: 'Returns all Revit grids in the active document with id, name, and curve data.',
    inputSchema: {
      type:       'object',
      properties: { ...activeViewScopeProps },
      required:   []
    }
  },

  // --- create_grid ---------------------------------------------------------
  {
    name:        'create_grid',
    description: 'Creates a straight Revit grid line. Coordinates default to feet.',
    inputSchema: {
      type:       'object',
      properties: {
        name: {
          type:        'string',
          description: 'Optional grid name. If omitted, Revit assigns a default name.'
        },
        startX: { type: 'number', description: 'Grid start X coordinate.' },
        startY: { type: 'number', description: 'Grid start Y coordinate.' },
        endX:   { type: 'number', description: 'Grid end X coordinate.' },
        endY:   { type: 'number', description: 'Grid end Y coordinate.' },
        z: {
          type:        'number',
          description: 'Optional Z coordinate for both endpoints. Default: 0.',
          default:     0
        },
        unit: {
          type:        'string',
          description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.',
          default:     'feet'
        }
      },
      required: ['startX', 'startY', 'endX', 'endY']
    }
  },

  // --- update_grid ---------------------------------------------------------
  {
    name:        'update_grid',
    description: 'Updates a Revit grid. Currently supports renaming the grid.',
    inputSchema: {
      type:       'object',
      properties: {
        gridId: {
          type:        'string',
          description: 'Revit ElementId of the grid to update.'
        },
        name: {
          type:        'string',
          description: 'New grid name.'
        }
      },
      required: ['gridId']
    }
  },

  // --- get_element_parameters ----------------------------------------------
  {
    name:        'get_element_parameters',
    description: 'Reads instance and optional type parameters for a Revit element.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: {
          type:        'string',
          description: 'Revit ElementId of the element to inspect.'
        },
        includeTypeParameters: {
          type:        'boolean',
          description: 'Include parameters from the element type. Default: true.',
          default:     true
        }
      },
      required: ['elementId']
    }
  },

  // --- set_element_parameter ------------------------------------------------
  {
    name:        'set_element_parameter',
    description: 'Sets one writable instance or type parameter on a Revit element.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: {
          type:        'string',
          description: 'Revit ElementId of the element to update.'
        },
        parameterName: {
          type:        'string',
          description: 'Parameter display name.'
        },
        value: {
          description: 'New parameter value. JSON type is converted based on the Revit storage type.'
        },
        target: {
          type:        'string',
          description: 'Parameter target: instance or type. Default: instance.',
          default:     'instance'
        }
      },
      required: ['elementId', 'parameterName', 'value']
    }
  },

  // --- set_element_parameters ----------------------------------------------
  {
    name:        'set_element_parameters',
    description: 'Sets multiple writable instance or type parameters on one Revit element.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: {
          type:        'string',
          description: 'Revit ElementId of the element to update.'
        },
        parameters: {
          type:        'object',
          description: 'Object whose keys are parameter display names and values are new parameter values.'
        },
        target: {
          type:        'string',
          description: 'Parameter target: instance or type. Default: instance.',
          default:     'instance'
        }
      },
      required: ['elementId', 'parameters']
    }
  },

  // --- batch_set_parameters -------------------------------------------------
  {
    name:        'batch_set_parameters',
    description: 'Sets the same parameter values on multiple Revit elements, with optional dry run.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: {
          type:        'array',
          description: 'Revit ElementIds to update.',
          items:       { type: 'string' }
        },
        parameters: {
          type:        'object',
          description: 'Object whose keys are parameter display names and values are new parameter values.'
        },
        target: {
          type:        'string',
          description: 'Parameter target: instance or type. Default: instance.',
          default:     'instance'
        },
        dryRun: {
          type:        'boolean',
          description: 'Validate parameter availability without writing changes. Default: false.',
          default:     false
        },
        maxItems: {
          type:        'integer',
          description: 'Safety cap for element count. Default: 100, hard max: 500.',
          default:     100
        }
      },
      required: ['elementIds', 'parameters']
    }
  },

  // --- get_views -----------------------------------------------------------
  {
    name:        'get_views',
    description: 'Returns Revit views with id, name, view type, template flag, scale, and level.',
    inputSchema: { type: 'object', properties: {}, required: [] }
  },

  // --- create_view ---------------------------------------------------------
  {
    name:        'create_view',
    description: 'Creates a Revit view. Supports floorPlan, ceilingPlan, structuralPlan, threeD, and drafting.',
    inputSchema: {
      type:       'object',
      properties: {
        viewType: { type: 'string', description: 'floorPlan, ceilingPlan, structuralPlan, threeD, or drafting.', default: 'floorPlan' },
        name: { type: 'string', description: 'Optional view name.' },
        levelId: { type: 'string', description: 'Required for plan views.' },
        viewFamilyTypeId: { type: 'string', description: 'Optional ViewFamilyType id.' },
        scale: { type: 'integer', description: 'Optional view scale.' }
      },
      required: []
    }
  },

  // --- duplicate_view ------------------------------------------------------
  {
    name:        'duplicate_view',
    description: 'Duplicates a Revit view using Duplicate, WithDetailing, or AsDependent.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Source view ElementId.' },
        name: { type: 'string', description: 'Optional new view name.' },
        duplicateOption: { type: 'string', description: 'Duplicate, WithDetailing, or AsDependent.', default: 'Duplicate' }
      },
      required: ['viewId']
    }
  },

  // --- update_view ---------------------------------------------------------
  {
    name:        'update_view',
    description: 'Updates a Revit view name, scale, and/or view template.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'View ElementId.' },
        name: { type: 'string', description: 'New view name.' },
        scale: { type: 'integer', description: 'New view scale.' },
        viewTemplateId: { type: 'string', description: "View template ElementId, 'none', or -1." }
      },
      required: ['viewId']
    }
  },

  // --- get_sheets ----------------------------------------------------------
  {
    name:        'get_sheets',
    description: 'Returns Revit sheets with id, sheet number, name, and placed views.',
    inputSchema: { type: 'object', properties: {}, required: [] }
  },

  // --- create_sheet --------------------------------------------------------
  {
    name:        'create_sheet',
    description: 'Creates a Revit sheet using a title block type id/name or the first available title block.',
    inputSchema: {
      type:       'object',
      properties: {
        sheetNumber: { type: 'string', description: 'Optional sheet number.' },
        sheetName: { type: 'string', description: 'Optional sheet name.' },
        titleBlockTypeId: { type: 'string', description: 'Optional title block FamilySymbol ElementId.' },
        titleBlockTypeName: { type: 'string', description: "Optional title block type name or 'Family: Type'." }
      },
      required: []
    }
  },

  // --- update_sheet --------------------------------------------------------
  {
    name:        'update_sheet',
    description: 'Updates a Revit sheet number and/or name.',
    inputSchema: {
      type:       'object',
      properties: {
        sheetId: { type: 'string', description: 'Sheet ElementId.' },
        sheetNumber: { type: 'string', description: 'New sheet number.' },
        sheetName: { type: 'string', description: 'New sheet name.' }
      },
      required: ['sheetId']
    }
  },

  // --- place_view_on_sheet -------------------------------------------------
  {
    name:        'place_view_on_sheet',
    description: 'Places a Revit view on a sheet by creating a viewport at a sheet coordinate.',
    inputSchema: {
      type:       'object',
      properties: {
        sheetId: { type: 'string', description: 'Target sheet ElementId.' },
        viewId: { type: 'string', description: 'View ElementId to place.' },
        x: { type: 'number', description: 'Sheet X coordinate.' },
        y: { type: 'number', description: 'Sheet Y coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        viewportTypeId: { type: 'string', description: 'Optional viewport type ElementId.' }
      },
      required: ['sheetId', 'viewId', 'x', 'y']
    }
  },

  // --- place_title_view_on_sheet ------------------------------------------
  {
    name:        'place_title_view_on_sheet',
    description: 'Places a drafting or legend view used as a title graphic on a sheet.',
    inputSchema: {
      type:       'object',
      properties: {
        sheetId: { type: 'string', description: 'Target sheet ElementId.' },
        viewId: { type: 'string', description: 'Drafting View or Legend ElementId to place.' },
        x: { type: 'number', description: 'Sheet X coordinate.' },
        y: { type: 'number', description: 'Sheet Y coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        viewportTypeId: { type: 'string', description: 'Optional viewport type ElementId.' }
      },
      required: ['sheetId', 'viewId', 'x', 'y']
    }
  },

  // --- remove_view_from_sheet ---------------------------------------------
  {
    name:        'remove_view_from_sheet',
    description: 'Removes a placed view from a sheet by viewportId or by sheetId + viewId.',
    inputSchema: {
      type:       'object',
      properties: {
        viewportId: { type: 'string', description: 'Viewport ElementId to delete.' },
        sheetId: { type: 'string', description: 'Sheet ElementId, used with viewId if viewportId is omitted.' },
        viewId: { type: 'string', description: 'View ElementId, used with sheetId if viewportId is omitted.' }
      },
      required: []
    }
  },

  // --- get_view_sheet_sets -------------------------------------------------
  {
    name:        'get_view_sheet_sets',
    description: 'Lists named Revit ViewSheetSets used by PrintManager.',
    inputSchema: { type: 'object', properties: {}, required: [] }
  },

  // --- create_view_sheet_set ----------------------------------------------
  {
    name:        'create_view_sheet_set',
    description: 'Creates a named ViewSheetSet from viewIds and/or sheetIds.',
    inputSchema: {
      type:       'object',
      properties: {
        name: { type: 'string', description: 'ViewSheetSet name.' },
        viewIds: { type: 'array', items: { type: 'string' }, description: 'View ids to include.' },
        sheetIds: { type: 'array', items: { type: 'string' }, description: 'Sheet ids to include.' },
        replaceExisting: { type: 'boolean', description: 'Replace an existing set with the same name.', default: false }
      },
      required: ['name']
    }
  },

  // --- add_views_to_view_sheet_set ----------------------------------------
  {
    name:        'add_views_to_view_sheet_set',
    description: 'Adds viewIds to an existing ViewSheetSet.',
    inputSchema: {
      type:       'object',
      properties: {
        name: { type: 'string', description: 'Existing ViewSheetSet name.' },
        viewIds: { type: 'array', items: { type: 'string' }, description: 'View ids to add.' }
      },
      required: ['name', 'viewIds']
    }
  },

  // --- add_sheets_to_view_sheet_set ---------------------------------------
  {
    name:        'add_sheets_to_view_sheet_set',
    description: 'Adds sheetIds to an existing ViewSheetSet.',
    inputSchema: {
      type:       'object',
      properties: {
        name: { type: 'string', description: 'Existing ViewSheetSet name.' },
        sheetIds: { type: 'array', items: { type: 'string' }, description: 'Sheet ids to add.' }
      },
      required: ['name', 'sheetIds']
    }
  },

  // --- remove_from_view_sheet_set -----------------------------------------
  {
    name:        'remove_from_view_sheet_set',
    description: 'Removes viewIds and/or sheetIds from an existing ViewSheetSet.',
    inputSchema: {
      type:       'object',
      properties: {
        name: { type: 'string', description: 'Existing ViewSheetSet name.' },
        viewIds: { type: 'array', items: { type: 'string' }, description: 'View ids to remove.' },
        sheetIds: { type: 'array', items: { type: 'string' }, description: 'Sheet ids to remove.' }
      },
      required: ['name']
    }
  },

  // --- delete_view_sheet_set ----------------------------------------------
  {
    name:        'delete_view_sheet_set',
    description: 'Deletes a named ViewSheetSet.',
    inputSchema: {
      type:       'object',
      properties: {
        name: { type: 'string', description: 'ViewSheetSet name to delete.' }
      },
      required: ['name']
    }
  },

  // --- get_element --------------------------------------------------------
  {
    name:        'get_element',
    description: 'Returns detailed metadata for one Revit element by ElementId.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Revit ElementId to inspect.' },
        includeParameters: { type: 'boolean', description: 'Include instance parameters. Default: false.', default: false },
        includeTypeParameters: { type: 'boolean', description: 'Include type parameters when includeParameters is true. Default: true.', default: true }
      },
      required: ['elementId']
    }
  },

  // --- get_element_location ----------------------------------------------
  {
    name:        'get_element_location',
    description: 'Returns the LocationPoint or LocationCurve data for one Revit element.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Revit ElementId to inspect.' }
      },
      required: ['elementId']
    }
  },

  // --- get_element_geometry_summary --------------------------------------
  {
    name:        'get_element_geometry_summary',
    description: 'Returns a lightweight geometry summary for one Revit element.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Revit ElementId to inspect.' },
        detailLevel: { type: 'string', description: 'Coarse, Medium, Fine, or Undefined. Default: Medium.', default: 'Medium' },
        includeNonVisibleObjects: { type: 'boolean', description: 'Include non-visible geometry objects. Default: false.', default: false }
      },
      required: ['elementId']
    }
  },

  // --- update_element -----------------------------------------------------
  {
    name:        'update_element',
    description: 'Updates general element fields: name, typeId, pinned state, and instance/type parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Revit ElementId to update.' },
        name: { type: 'string', description: 'Optional new element name where writable.' },
        typeId: { type: 'string', description: 'Optional target ElementType id.' },
        pinned: { type: 'boolean', description: 'Optional pinned state.' },
        parameters: { type: 'object', description: 'Optional object of parameter display names to new values.' },
        parameterTarget: { type: 'string', description: 'Parameter target: instance or type. Default: instance.', default: 'instance' }
      },
      required: ['elementId']
    }
  },

  // --- change_element_type -----------------------------------------------
  {
    name:        'change_element_type',
    description: 'Changes one element to another compatible Revit ElementType.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Revit ElementId to update.' },
        typeId: { type: 'string', description: 'Target ElementType id.' }
      },
      required: ['elementId', 'typeId']
    }
  },

  // --- create_family_instance --------------------------------------------
  {
    name:        'create_family_instance',
    description: 'Creates a Revit FamilyInstance from a FamilySymbol at a point, with optional level, host, structural type, and parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId for level-based families.' },
        hostId: { type: 'string', description: 'Optional host ElementId for hosted families.' },
        structuralType: { type: 'string', description: 'NonStructural, Beam, Brace, Column, or Footing. Default: NonStructural.', default: 'NonStructural' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_door ---------------------------------------------------------
  {
    name:        'place_door',
    description: 'Places a door FamilySymbol. Provide a hostId for hosted door families.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'Door FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional wall host ElementId.' },
        structuralType: { type: 'string', description: 'Default: NonStructural.', default: 'NonStructural' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_window -------------------------------------------------------
  {
    name:        'place_window',
    description: 'Places a window FamilySymbol. Provide a hostId for hosted window families.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'Window FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional wall host ElementId.' },
        structuralType: { type: 'string', description: 'Default: NonStructural.', default: 'NonStructural' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_column -------------------------------------------------------
  {
    name:        'place_column',
    description: 'Places a column FamilySymbol. Defaults structuralType to Column.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'Column FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional host ElementId.' },
        structuralType: { type: 'string', description: 'Default: Column.', default: 'Column' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_structural_framing ------------------------------------------
  {
    name:        'place_structural_framing',
    description: 'Places a structural framing FamilySymbol. Defaults structuralType to Beam.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'Structural framing FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional host ElementId.' },
        structuralType: { type: 'string', description: 'Default: Beam.', default: 'Beam' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_furniture ----------------------------------------------------
  {
    name:        'place_furniture',
    description: 'Places a furniture FamilySymbol.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'Furniture FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional host ElementId.' },
        structuralType: { type: 'string', description: 'Default: NonStructural.', default: 'NonStructural' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_equipment ----------------------------------------------------
  {
    name:        'place_equipment',
    description: 'Places an equipment FamilySymbol. Use list_family_types to locate the correct symbolId.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'Equipment FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional host ElementId.' },
        structuralType: { type: 'string', description: 'Default: NonStructural.', default: 'NonStructural' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_mep_fixture --------------------------------------------------
  {
    name:        'place_mep_fixture',
    description: 'Places an MEP fixture FamilySymbol.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional host ElementId.' },
        structuralType: { type: 'string', description: 'Default: NonStructural.', default: 'NonStructural' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_mechanical_equipment ----------------------------------------
  {
    name:        'place_mechanical_equipment',
    description: 'Places a mechanical equipment FamilySymbol.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'Mechanical equipment FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional host ElementId.' },
        structuralType: { type: 'string', description: 'Default: NonStructural.', default: 'NonStructural' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- place_electrical_equipment ----------------------------------------
  {
    name:        'place_electrical_equipment',
    description: 'Places an electrical equipment FamilySymbol.',
    inputSchema: {
      type:       'object',
      properties: {
        symbolId: { type: 'string', description: 'Electrical equipment FamilySymbol ElementId to place.' },
        x: { type: 'number', description: 'Location X.' },
        y: { type: 'number', description: 'Location Y.' },
        z: { type: 'number', description: 'Location Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        levelId: { type: 'string', description: 'Optional Level ElementId.' },
        hostId: { type: 'string', description: 'Optional host ElementId.' },
        structuralType: { type: 'string', description: 'Default: NonStructural.', default: 'NonStructural' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['symbolId', 'x', 'y', 'z']
    }
  },

  // --- get_connectors -----------------------------------------------------
  {
    name:        'get_connectors',
    description: 'Lists MEP connectors for a MEPCurve or MEP FamilyInstance.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'ElementId to inspect.' }
      },
      required: ['elementId']
    }
  },

  // --- create_pipe --------------------------------------------------------
  {
    name:        'create_pipe',
    description: 'Creates a pipe between two points.',
    inputSchema: {
      type:       'object',
      properties: {
        typeId: { type: 'string', description: 'Optional PipeType id.' },
        systemTypeId: { type: 'string', description: 'Optional PipingSystemType id.' },
        levelId: { type: 'string', description: 'Level ElementId.' },
        startX: { type: 'number' }, startY: { type: 'number' }, startZ: { type: 'number' },
        endX: { type: 'number' }, endY: { type: 'number' }, endZ: { type: 'number' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['levelId', 'startX', 'startY', 'startZ', 'endX', 'endY', 'endZ']
    }
  },

  // --- update_pipe --------------------------------------------------------
  {
    name:        'update_pipe',
    description: 'Updates a pipe type, endpoints, and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Pipe ElementId.' },
        typeId: { type: 'string', description: 'Optional PipeType id.' },
        startX: { type: 'number' }, startY: { type: 'number' }, startZ: { type: 'number' },
        endX: { type: 'number' }, endY: { type: 'number' }, endZ: { type: 'number' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_duct --------------------------------------------------------
  {
    name:        'create_duct',
    description: 'Creates a duct between two points.',
    inputSchema: {
      type:       'object',
      properties: {
        typeId: { type: 'string', description: 'Optional DuctType id.' },
        systemTypeId: { type: 'string', description: 'Optional MechanicalSystemType id.' },
        levelId: { type: 'string', description: 'Level ElementId.' },
        startX: { type: 'number' }, startY: { type: 'number' }, startZ: { type: 'number' },
        endX: { type: 'number' }, endY: { type: 'number' }, endZ: { type: 'number' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['levelId', 'startX', 'startY', 'startZ', 'endX', 'endY', 'endZ']
    }
  },

  // --- update_duct --------------------------------------------------------
  {
    name:        'update_duct',
    description: 'Updates a duct type, endpoints, and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Duct ElementId.' },
        typeId: { type: 'string', description: 'Optional DuctType id.' },
        startX: { type: 'number' }, startY: { type: 'number' }, startZ: { type: 'number' },
        endX: { type: 'number' }, endY: { type: 'number' }, endZ: { type: 'number' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_conduit -----------------------------------------------------
  {
    name:        'create_conduit',
    description: 'Creates a conduit between two points.',
    inputSchema: {
      type:       'object',
      properties: {
        typeId: { type: 'string', description: 'Optional ConduitType id.' },
        levelId: { type: 'string', description: 'Level ElementId.' },
        startX: { type: 'number' }, startY: { type: 'number' }, startZ: { type: 'number' },
        endX: { type: 'number' }, endY: { type: 'number' }, endZ: { type: 'number' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['levelId', 'startX', 'startY', 'startZ', 'endX', 'endY', 'endZ']
    }
  },

  // --- update_conduit -----------------------------------------------------
  {
    name:        'update_conduit',
    description: 'Updates a conduit type, endpoints, and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Conduit ElementId.' },
        typeId: { type: 'string', description: 'Optional ConduitType id.' },
        startX: { type: 'number' }, startY: { type: 'number' }, startZ: { type: 'number' },
        endX: { type: 'number' }, endY: { type: 'number' }, endZ: { type: 'number' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_cable_tray --------------------------------------------------
  {
    name:        'create_cable_tray',
    description: 'Creates a cable tray between two points.',
    inputSchema: {
      type:       'object',
      properties: {
        typeId: { type: 'string', description: 'Optional CableTrayType id.' },
        levelId: { type: 'string', description: 'Level ElementId.' },
        startX: { type: 'number' }, startY: { type: 'number' }, startZ: { type: 'number' },
        endX: { type: 'number' }, endY: { type: 'number' }, endZ: { type: 'number' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['levelId', 'startX', 'startY', 'startZ', 'endX', 'endY', 'endZ']
    }
  },

  // --- update_cable_tray --------------------------------------------------
  {
    name:        'update_cable_tray',
    description: 'Updates a cable tray type, endpoints, and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'CableTray ElementId.' },
        typeId: { type: 'string', description: 'Optional CableTrayType id.' },
        startX: { type: 'number' }, startY: { type: 'number' }, startZ: { type: 'number' },
        endX: { type: 'number' }, endY: { type: 'number' }, endZ: { type: 'number' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- connect_mep_elements ----------------------------------------------
  {
    name:        'connect_mep_elements',
    description: 'Connects two MEP connectors by index or nearest XYZ.',
    inputSchema: {
      type:       'object',
      properties: {
        first: { type: 'object', properties: { elementId: { type: 'string' }, connectorIndex: { type: 'integer' }, x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } }, required: ['elementId'] },
        second: { type: 'object', properties: { elementId: { type: 'string' }, connectorIndex: { type: 'integer' }, x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } }, required: ['elementId'] },
        unit: { type: 'string', description: 'Coordinate unit for XYZ connector references. Default: feet.', default: 'feet' }
      },
      required: ['first', 'second']
    }
  },

  // --- disconnect_mep_elements -------------------------------------------
  {
    name:        'disconnect_mep_elements',
    description: 'Disconnects two MEP connectors by index or nearest XYZ.',
    inputSchema: {
      type:       'object',
      properties: {
        first: { type: 'object', properties: { elementId: { type: 'string' }, connectorIndex: { type: 'integer' }, x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } }, required: ['elementId'] },
        second: { type: 'object', properties: { elementId: { type: 'string' }, connectorIndex: { type: 'integer' }, x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } }, required: ['elementId'] },
        unit: { type: 'string', description: 'Coordinate unit for XYZ connector references. Default: feet.', default: 'feet' }
      },
      required: ['first', 'second']
    }
  },

  // --- delete_elements ----------------------------------------------------
  {
    name:        'delete_elements',
    description: 'Deletes Revit elements by ElementId, with dryRun and maxItems safety controls.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'Revit ElementIds to delete.', items: { type: 'string' } },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds']
    }
  },

  // --- move_elements ------------------------------------------------------
  {
    name:        'move_elements',
    description: 'Moves Revit elements by a translation vector.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'Revit ElementIds to move.', items: { type: 'string' } },
        x: { type: 'number', description: 'Translation X.' },
        y: { type: 'number', description: 'Translation Y.' },
        z: { type: 'number', description: 'Translation Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds', 'x', 'y', 'z']
    }
  },

  // --- copy_elements ------------------------------------------------------
  {
    name:        'copy_elements',
    description: 'Copies Revit elements by a translation vector and returns the copied ElementIds.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'Revit ElementIds to copy.', items: { type: 'string' } },
        x: { type: 'number', description: 'Translation X.' },
        y: { type: 'number', description: 'Translation Y.' },
        z: { type: 'number', description: 'Translation Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds', 'x', 'y', 'z']
    }
  },

  // --- rotate_elements ----------------------------------------------------
  {
    name:        'rotate_elements',
    description: 'Rotates Revit elements around an axis defined by origin and vector.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'Revit ElementIds to rotate.', items: { type: 'string' } },
        originX: { type: 'number', description: 'Axis origin X.' },
        originY: { type: 'number', description: 'Axis origin Y.' },
        originZ: { type: 'number', description: 'Axis origin Z.' },
        axisX: { type: 'number', description: 'Axis vector X.' },
        axisY: { type: 'number', description: 'Axis vector Y.' },
        axisZ: { type: 'number', description: 'Axis vector Z.', default: 1 },
        angleDegrees: { type: 'number', description: 'Rotation angle in degrees.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds', 'originX', 'originY', 'originZ', 'axisX', 'axisY', 'axisZ', 'angleDegrees']
    }
  },

  // --- mirror_elements ----------------------------------------------------
  {
    name:        'mirror_elements',
    description: 'Mirrors Revit elements across a plane defined by origin and normal.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'Revit ElementIds to mirror.', items: { type: 'string' } },
        originX: { type: 'number', description: 'Mirror plane origin X.' },
        originY: { type: 'number', description: 'Mirror plane origin Y.' },
        originZ: { type: 'number', description: 'Mirror plane origin Z.' },
        normalX: { type: 'number', description: 'Mirror plane normal X.', default: 1 },
        normalY: { type: 'number', description: 'Mirror plane normal Y.' },
        normalZ: { type: 'number', description: 'Mirror plane normal Z.' },
        copy: { type: 'boolean', description: 'Create mirrored copies instead of mirroring originals. Default: true.', default: true },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds', 'originX', 'originY', 'originZ', 'normalX', 'normalY', 'normalZ']
    }
  },

  // --- pin_elements -------------------------------------------------------
  {
    name:        'pin_elements',
    description: 'Pins Revit elements by ElementId.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'Revit ElementIds to pin.', items: { type: 'string' } },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds']
    }
  },

  // --- unpin_elements -----------------------------------------------------
  {
    name:        'unpin_elements',
    description: 'Unpins Revit elements by ElementId.',
    inputSchema: {
      type:       'object',
      properties: {
        elementIds: { type: 'array', description: 'Revit ElementIds to unpin.', items: { type: 'string' } },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds']
    }
  },

  // --- hide_elements_in_view ---------------------------------------------
  {
    name:        'hide_elements_in_view',
    description: 'Permanently hides elements in a target view, or the active view when viewId is omitted.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Optional target View ElementId. Uses active view when omitted.' },
        elementIds: { type: 'array', description: 'Revit ElementIds to hide in the view.', items: { type: 'string' } },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds']
    }
  },

  // --- unhide_elements_in_view -------------------------------------------
  {
    name:        'unhide_elements_in_view',
    description: 'Unhides elements in a target view, or the active view when viewId is omitted.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Optional target View ElementId. Uses active view when omitted.' },
        elementIds: { type: 'array', description: 'Revit ElementIds to unhide in the view.', items: { type: 'string' } },
        dryRun: { type: 'boolean', description: 'Validate inputs without modifying the model. Default: false.', default: false },
        maxItems: { type: 'integer', description: 'Safety cap. Default: 100, hard max: 500.', default: 100 }
      },
      required: ['elementIds']
    }
  },

  // --- validate_batch_operations -----------------------------------------
  {
    name:        'validate_batch_operations',
    description: 'Validates a list of supported element operations without modifying the Revit model.',
    inputSchema: {
      type:       'object',
      properties: {
        operations: {
          type:        'array',
          description: 'Batch items. Each item accepts operation plus either an arguments object or flattened arguments.',
          items: {
            type:       'object',
            properties: {
              operation: {
                type:        'string',
                description: 'Supported: create_family_instance, update_element, change_element_type, delete_elements, move_elements, copy_elements, rotate_elements, mirror_elements, pin_elements, unpin_elements, hide_elements_in_view, unhide_elements_in_view.'
              },
              arguments: {
                type:        'object',
                description: 'Arguments matching the selected operation. Flattened arguments are also accepted.'
              }
            },
            required: ['operation']
          }
        },
        dryRun: { type: 'boolean', description: 'Ignored by validate_batch_operations; kept for schema compatibility.', default: true },
        continueOnError: { type: 'boolean', description: 'Continue validating after a failed operation. Default: false.', default: false },
        maxOperations: { type: 'integer', description: 'Safety cap. Default: 25, hard max: 100.', default: 25 }
      },
      required: ['operations']
    }
  },

  // --- apply_batch_operations --------------------------------------------
  {
    name:        'apply_batch_operations',
    description: 'Applies a list of supported element operations. Use dryRun=true to validate without changing the model.',
    inputSchema: {
      type:       'object',
      properties: {
        operations: {
          type:        'array',
          description: 'Batch items. Each item accepts operation plus either an arguments object or flattened arguments.',
          items: {
            type:       'object',
            properties: {
              operation: {
                type:        'string',
                description: 'Supported: create_family_instance, update_element, change_element_type, delete_elements, move_elements, copy_elements, rotate_elements, mirror_elements, pin_elements, unpin_elements, hide_elements_in_view, unhide_elements_in_view.'
              },
              arguments: {
                type:        'object',
                description: 'Arguments matching the selected operation. Flattened arguments are also accepted.'
              }
            },
            required: ['operation']
          }
        },
        dryRun: { type: 'boolean', description: 'Validate without modifying the model. Default: false.', default: false },
        continueOnError: { type: 'boolean', description: 'Continue after a failed operation. Default: false.', default: false },
        maxOperations: { type: 'integer', description: 'Safety cap. Default: 25, hard max: 100.', default: 25 }
      },
      required: ['operations']
    }
  },

  // --- create_material ----------------------------------------------------
  {
    name:        'create_material',
    description: 'Creates a Revit material with optional RGB color and transparency.',
    inputSchema: {
      type:       'object',
      properties: {
        name: { type: 'string', description: 'New material name.' },
        color: {
          type:       'object',
          properties: {
            r: { type: 'integer', description: 'Red 0-255.' },
            g: { type: 'integer', description: 'Green 0-255.' },
            b: { type: 'integer', description: 'Blue 0-255.' }
          },
          required: ['r', 'g', 'b']
        },
        transparency: { type: 'integer', description: 'Transparency 0-100.' }
      },
      required: ['name']
    }
  },

  // --- update_material ----------------------------------------------------
  {
    name:        'update_material',
    description: 'Updates a Revit material by materialId or name.',
    inputSchema: {
      type:       'object',
      properties: {
        materialId: { type: 'string', description: 'Existing Material ElementId.' },
        name: { type: 'string', description: 'Existing material name if materialId is omitted.' },
        newName: { type: 'string', description: 'Optional new material name.' },
        color: {
          type:       'object',
          properties: {
            r: { type: 'integer', description: 'Red 0-255.' },
            g: { type: 'integer', description: 'Green 0-255.' },
            b: { type: 'integer', description: 'Blue 0-255.' }
          },
          required: ['r', 'g', 'b']
        },
        transparency: { type: 'integer', description: 'Transparency 0-100.' }
      },
      required: []
    }
  },

  // --- duplicate_element_type --------------------------------------------
  {
    name:        'duplicate_element_type',
    description: 'Duplicates an ElementType and returns the new type id.',
    inputSchema: {
      type:       'object',
      properties: {
        typeId: { type: 'string', description: 'Source ElementType id.' },
        name: { type: 'string', description: 'New type name.' }
      },
      required: ['typeId', 'name']
    }
  },

  // --- rename_element_type -----------------------------------------------
  {
    name:        'rename_element_type',
    description: 'Renames an ElementType.',
    inputSchema: {
      type:       'object',
      properties: {
        typeId: { type: 'string', description: 'ElementType id to rename.' },
        name: { type: 'string', description: 'New type name.' }
      },
      required: ['typeId', 'name']
    }
  },

  // --- set_type_parameter -------------------------------------------------
  {
    name:        'set_type_parameter',
    description: 'Sets one writable parameter on an ElementType.',
    inputSchema: {
      type:       'object',
      properties: {
        typeId: { type: 'string', description: 'ElementType id.' },
        parameterName: { type: 'string', description: 'Parameter display name.' },
        value: { description: 'New parameter value.' }
      },
      required: ['typeId', 'parameterName', 'value']
    }
  },

  // --- set_type_parameters ------------------------------------------------
  {
    name:        'set_type_parameters',
    description: 'Sets multiple writable parameters on an ElementType.',
    inputSchema: {
      type:       'object',
      properties: {
        typeId: { type: 'string', description: 'ElementType id.' },
        parameters: { type: 'object', description: 'Object whose keys are parameter names and values are new values.' }
      },
      required: ['typeId', 'parameters']
    }
  },

  // --- apply_view_template ------------------------------------------------
  {
    name:        'apply_view_template',
    description: 'Applies a view template to a Revit view.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view id.' },
        viewTemplateId: { type: 'string', description: 'View template id.' }
      },
      required: ['viewId', 'viewTemplateId']
    }
  },

  // --- create_view_template ----------------------------------------------
  {
    name:        'create_view_template',
    description: 'Creates a view template from an existing Revit view.',
    inputSchema: {
      type:       'object',
      properties: {
        sourceViewId: { type: 'string', description: 'Source view id.' },
        name: { type: 'string', description: 'Optional template name.' }
      },
      required: ['sourceViewId']
    }
  },

  // --- create_text_note ---------------------------------------------------
  {
    name:        'create_text_note',
    description: 'Creates a Revit TextNote in a view.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view id.' },
        text: { type: 'string', description: 'Text content.' },
        x: { type: 'number', description: 'X coordinate.' },
        y: { type: 'number', description: 'Y coordinate.' },
        z: { type: 'number', description: 'Z coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        textNoteTypeId: { type: 'string', description: 'Optional TextNoteType id.' }
      },
      required: ['viewId', 'text', 'x', 'y', 'z']
    }
  },

  // --- update_text_note ---------------------------------------------------
  {
    name:        'update_text_note',
    description: 'Updates text, position, and/or type of a Revit TextNote.',
    inputSchema: {
      type:       'object',
      properties: {
        textNoteId: { type: 'string', description: 'TextNote id for update.' },
        text: { type: 'string', description: 'Text content.' },
        x: { type: 'number', description: 'Optional X coordinate.' },
        y: { type: 'number', description: 'Optional Y coordinate.' },
        z: { type: 'number', description: 'Optional Z coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        textNoteTypeId: { type: 'string', description: 'Optional TextNoteType id.' }
      },
      required: ['textNoteId']
    }
  },

  // --- create_detail_line -------------------------------------------------
  {
    name:        'create_detail_line',
    description: 'Creates a detail line in a Revit view.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view id.' },
        startX: { type: 'number', description: 'Start X.' },
        startY: { type: 'number', description: 'Start Y.' },
        startZ: { type: 'number', description: 'Start Z.' },
        endX: { type: 'number', description: 'End X.' },
        endY: { type: 'number', description: 'End Y.' },
        endZ: { type: 'number', description: 'End Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' }
      },
      required: ['viewId', 'startX', 'startY', 'startZ', 'endX', 'endY', 'endZ']
    }
  },

  // --- create_model_line --------------------------------------------------
  {
    name:        'create_model_line',
    description: 'Creates a model line on a horizontal sketch plane.',
    inputSchema: {
      type:       'object',
      properties: {
        startX: { type: 'number', description: 'Start X.' },
        startY: { type: 'number', description: 'Start Y.' },
        startZ: { type: 'number', description: 'Start Z.' },
        endX: { type: 'number', description: 'End X.' },
        endY: { type: 'number', description: 'End Y.' },
        endZ: { type: 'number', description: 'End Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' }
      },
      required: ['startX', 'startY', 'startZ', 'endX', 'endY', 'endZ']
    }
  },

  // --- update_wall --------------------------------------------------------
  {
    name:        'update_wall',
    description: 'Updates a wall type and/or instance parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Wall ElementId.' },
        typeId: { type: 'string', description: 'Optional target WallType id.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_floor -------------------------------------------------------
  {
    name:        'create_floor',
    description: 'Creates a floor from a closed polygon boundary.',
    inputSchema: {
      type:       'object',
      properties: {
        levelId: { type: 'string', description: 'Target Level ElementId.' },
        typeId: { type: 'string', description: 'Optional FloorType id.' },
        points: {
          type:        'array',
          description: 'Closed boundary points; do not repeat the first point at the end.',
          items: {
            type:       'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            required: ['x', 'y', 'z']
          }
        },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['levelId', 'points']
    }
  },

  // --- update_floor -------------------------------------------------------
  {
    name:        'update_floor',
    description: 'Updates a floor type and/or instance parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Floor ElementId.' },
        typeId: { type: 'string', description: 'Optional target FloorType id.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_ceiling -----------------------------------------------------
  {
    name:        'create_ceiling',
    description: 'Creates a ceiling from a closed polygon boundary.',
    inputSchema: {
      type:       'object',
      properties: {
        levelId: { type: 'string', description: 'Target Level ElementId.' },
        typeId: { type: 'string', description: 'Optional CeilingType id.' },
        points: {
          type:        'array',
          description: 'Closed boundary points; do not repeat the first point at the end.',
          items: {
            type:       'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            required: ['x', 'y', 'z']
          }
        },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['levelId', 'points']
    }
  },

  // --- update_ceiling -----------------------------------------------------
  {
    name:        'update_ceiling',
    description: 'Updates a ceiling type and/or instance parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Ceiling ElementId.' },
        typeId: { type: 'string', description: 'Optional target CeilingType id.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_room --------------------------------------------------------
  {
    name:        'create_room',
    description: 'Creates a room at a level and XY point.',
    inputSchema: {
      type:       'object',
      properties: {
        levelId: { type: 'string', description: 'Target Level ElementId.' },
        x: { type: 'number', description: 'Room placement X.' },
        y: { type: 'number', description: 'Room placement Y.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        name: { type: 'string', description: 'Optional room name.' },
        number: { type: 'string', description: 'Optional room number.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['levelId', 'x', 'y']
    }
  },

  // --- update_room --------------------------------------------------------
  {
    name:        'update_room',
    description: 'Updates room name, number, and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        roomId: { type: 'string', description: 'Room ElementId.' },
        name: { type: 'string', description: 'Optional room name.' },
        number: { type: 'string', description: 'Optional room number.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['roomId']
    }
  },

  // --- create_roof --------------------------------------------------------
  {
    name:        'create_roof',
    description: 'Creates a footprint roof from a closed polygon boundary.',
    inputSchema: {
      type:       'object',
      properties: {
        levelId: { type: 'string', description: 'Target Level ElementId.' },
        roofTypeId: { type: 'string', description: 'Optional RoofType id.' },
        points: {
          type:        'array',
          description: 'Closed boundary points; do not repeat the first point at the end.',
          items: {
            type:       'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            required: ['x', 'y', 'z']
          }
        },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set after creation.' }
      },
      required: ['levelId', 'points']
    }
  },

  // --- update_roof --------------------------------------------------------
  {
    name:        'update_roof',
    description: 'Updates a roof type and/or instance parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Roof ElementId.' },
        typeId: { type: 'string', description: 'Optional target RoofType id.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_opening -----------------------------------------------------
  {
    name:        'create_opening',
    description: 'Creates an opening on a host element from a bounding box/profile.',
    inputSchema: {
      type:       'object',
      properties: {
        hostId: { type: 'string', description: 'Host ElementId.' },
        minX: { type: 'number', description: 'Minimum X coordinate.' },
        minY: { type: 'number', description: 'Minimum Y coordinate.' },
        minZ: { type: 'number', description: 'Minimum Z coordinate.' },
        maxX: { type: 'number', description: 'Maximum X coordinate.' },
        maxY: { type: 'number', description: 'Maximum Y coordinate.' },
        maxZ: { type: 'number', description: 'Maximum Z coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['hostId', 'minX', 'minY', 'minZ', 'maxX', 'maxY', 'maxZ']
    }
  },

  // --- update_opening -----------------------------------------------------
  {
    name:        'update_opening',
    description: 'Updates an opening type and/or instance parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Opening ElementId.' },
        typeId: { type: 'string', description: 'Optional target type id when supported.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_area --------------------------------------------------------
  {
    name:        'create_area',
    description: 'Creates an area in an area plan view at an XY point.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Area plan View ElementId.' },
        x: { type: 'number', description: 'Placement X coordinate.' },
        y: { type: 'number', description: 'Placement Y coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        name: { type: 'string', description: 'Optional area name.' },
        number: { type: 'string', description: 'Optional area number.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['viewId', 'x', 'y']
    }
  },

  // --- update_area --------------------------------------------------------
  {
    name:        'update_area',
    description: 'Updates area name, number, and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Area ElementId.' },
        name: { type: 'string', description: 'Optional area name.' },
        number: { type: 'string', description: 'Optional area number.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_space -------------------------------------------------------
  {
    name:        'create_space',
    description: 'Creates an MEP space at a level and XY point.',
    inputSchema: {
      type:       'object',
      properties: {
        levelId: { type: 'string', description: 'Target Level ElementId.' },
        x: { type: 'number', description: 'Placement X coordinate.' },
        y: { type: 'number', description: 'Placement Y coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        name: { type: 'string', description: 'Optional space name.' },
        number: { type: 'string', description: 'Optional space number.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['levelId', 'x', 'y']
    }
  },

  // --- update_space -------------------------------------------------------
  {
    name:        'update_space',
    description: 'Updates space name, number, and/or parameters.',
    inputSchema: {
      type:       'object',
      properties: {
        elementId: { type: 'string', description: 'Space ElementId.' },
        name: { type: 'string', description: 'Optional space name.' },
        number: { type: 'string', description: 'Optional space number.' },
        parameters: { type: 'object', description: 'Optional instance parameters to set.' }
      },
      required: ['elementId']
    }
  },

  // --- create_tag ---------------------------------------------------------
  {
    name:        'create_tag',
    description: 'Creates an IndependentTag for an element in a view.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view ElementId.' },
        elementId: { type: 'string', description: 'ElementId to tag.' },
        x: { type: 'number', description: 'Tag head X coordinate.' },
        y: { type: 'number', description: 'Tag head Y coordinate.' },
        z: { type: 'number', description: 'Tag head Z coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        tagTypeId: { type: 'string', description: 'Optional tag type id.' },
        addLeader: { type: 'boolean', description: 'Add a leader. Default: false.', default: false }
      },
      required: ['viewId', 'elementId', 'x', 'y', 'z']
    }
  },

  // --- place_room_tag -----------------------------------------------------
  {
    name:        'place_room_tag',
    description: 'Places a room tag in a view.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view ElementId.' },
        elementId: { type: 'string', description: 'Room ElementId.' },
        x: { type: 'number', description: 'Tag head X coordinate.' },
        y: { type: 'number', description: 'Tag head Y coordinate.' },
        z: { type: 'number', description: 'Tag head Z coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        tagTypeId: { type: 'string', description: 'Optional room tag type id.' },
        addLeader: { type: 'boolean', description: 'Add a leader. Default: false.', default: false }
      },
      required: ['viewId', 'elementId', 'x', 'y', 'z']
    }
  },

  // --- place_area_tag -----------------------------------------------------
  {
    name:        'place_area_tag',
    description: 'Places an area tag in a view.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view ElementId.' },
        elementId: { type: 'string', description: 'Area ElementId.' },
        x: { type: 'number', description: 'Tag head X coordinate.' },
        y: { type: 'number', description: 'Tag head Y coordinate.' },
        z: { type: 'number', description: 'Tag head Z coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        tagTypeId: { type: 'string', description: 'Optional area tag type id.' },
        addLeader: { type: 'boolean', description: 'Add a leader. Default: false.', default: false }
      },
      required: ['viewId', 'elementId', 'x', 'y', 'z']
    }
  },

  // --- place_space_tag ----------------------------------------------------
  {
    name:        'place_space_tag',
    description: 'Places a space tag in a view.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view ElementId.' },
        elementId: { type: 'string', description: 'Space ElementId.' },
        x: { type: 'number', description: 'Tag head X coordinate.' },
        y: { type: 'number', description: 'Tag head Y coordinate.' },
        z: { type: 'number', description: 'Tag head Z coordinate.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        tagTypeId: { type: 'string', description: 'Optional space tag type id.' },
        addLeader: { type: 'boolean', description: 'Add a leader. Default: false.', default: false }
      },
      required: ['viewId', 'elementId', 'x', 'y', 'z']
    }
  },

  // --- create_dimension ---------------------------------------------------
  {
    name:        'create_dimension',
    description: 'Creates a dimension between element references in a view.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view ElementId.' },
        elementIds: { type: 'array', description: 'ElementIds to dimension.', items: { type: 'string' } },
        startX: { type: 'number', description: 'Dimension line start X.' },
        startY: { type: 'number', description: 'Dimension line start Y.' },
        startZ: { type: 'number', description: 'Dimension line start Z.' },
        endX: { type: 'number', description: 'Dimension line end X.' },
        endY: { type: 'number', description: 'Dimension line end Y.' },
        endZ: { type: 'number', description: 'Dimension line end Z.' },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' },
        dimensionTypeId: { type: 'string', description: 'Optional DimensionType id.' }
      },
      required: ['viewId', 'elementIds', 'startX', 'startY', 'startZ', 'endX', 'endY', 'endZ']
    }
  },

  // --- create_filled_region ----------------------------------------------
  {
    name:        'create_filled_region',
    description: 'Creates a filled region in a view from a closed polygon.',
    inputSchema: {
      type:       'object',
      properties: {
        viewId: { type: 'string', description: 'Target view ElementId.' },
        filledRegionTypeId: { type: 'string', description: 'Optional FilledRegionType id.' },
        points: {
          type:        'array',
          description: 'Closed boundary points; do not repeat the first point at the end.',
          items: {
            type:       'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            required: ['x', 'y', 'z']
          }
        },
        unit: { type: 'string', description: 'Coordinate unit: feet, meters, or millimeters. Default: feet.', default: 'feet' }
      },
      required: ['viewId', 'points']
    }
  },

  // --- create_wall ---------------------------------------------------------
  {
    name:        'create_wall',
    description: 'Creates a straight wall in the active Revit document. ' +
                 'All coordinates are in Revit internal units (decimal feet). ' +
                 '1 metre ≈ 3.281 feet.',
    inputSchema: {
      type:       'object',
      properties: {
        startX: {
          type:        'number',
          description: 'Wall start point X coordinate (feet).'
        },
        startY: {
          type:        'number',
          description: 'Wall start point Y coordinate (feet).'
        },
        endX: {
          type:        'number',
          description: 'Wall end point X coordinate (feet).'
        },
        endY: {
          type:        'number',
          description: 'Wall end point Y coordinate (feet).'
        },
        height: {
          type:        'number',
          description: 'Wall height in feet (e.g. 9.84 ≈ 3 m).',
          minimum:     0.1
        },
        levelName: {
          type:        'string',
          description: 'Name of the Revit level (e.g. "Level 1"). Uses the first available level if omitted.',
          default:     'Level 1'
        },
        wallTypeName: {
          type:        'string',
          description: 'Wall type name (e.g. "Basic Wall"). Uses the project default if omitted.'
        }
      },
      required: ['startX', 'startY', 'endX', 'endY', 'height']
    }
  },

  // ── print_sheet_to_pdf ────────────────────────────────────
  {
    name:        'print_sheet_to_pdf',
    description: 'Export a single Revit sheet to a PDF file using PDF24 virtual printer. ' +
                 'The sheet is identified by its Revit element ID — use get_elements with ' +
                 'category="Sheets" to discover IDs. ' +
                 'The PDF is named after the sheet\'s SheetNumber. ' +
                 'Paper size is auto-detected from the title block dimensions.',
    inputSchema: {
      type:       'object',
      properties: {
        sheetId: {
          type:        'string',
          description: 'Revit element ID of the sheet to print (e.g. "123456"). ' +
                       'Required — use get_elements with category="Sheets" to get valid IDs.'
        },
        outputFolder: {
          type:        'string',
          description: 'Absolute folder path where PDF files will be saved. ' +
                       'Defaults to the document\'s own folder, or Desktop if unsaved.'
        },
        colorMode: {
          type:        'string',
          description: 'Color output: "Color" (default), "GrayScale", "BlackAndWhite".',
          default:     'Color'
        },
        rasterQuality: {
          type:        'string',
          description: 'Raster image quality: "Draft", "Low", "Medium", "High" (default), "Presentation".',
          default:     'High'
        }
      },
      required: ['sheetId']
    }
  },

  // ── export_sheet_to_cad ───────────────────────────────────
  {
    name:        'export_sheet_to_cad',
    description: 'Export a single Revit sheet to a DWG or DXF file. ' +
                 'Supports named export templates (ExportDWGSettings) stored in the Revit file. ' +
                 'The sheet is identified by its Revit element ID — use get_elements with ' +
                 'category="Sheets" to discover IDs. ' +
                 'Use list_cad_export_templates to see available template names. ' +
                 'The output file is named after the sheet\'s SheetNumber.',
    inputSchema: {
      type:       'object',
      properties: {
        sheetId: {
          type:        'string',
          description: 'Revit element ID of the sheet to export (e.g. "123456"). ' +
                       'Required — use get_elements with category="Sheets" to get valid IDs.'
        },
        outputFolder: {
          type:        'string',
          description: 'Absolute folder path where DWG/DXF files will be saved. ' +
                       'Defaults to the document\'s own folder, or Desktop if unsaved.'
        },
        templateName: {
          type:        'string',
          description: 'Name of a named DWG export template stored in the Revit file. ' +
                       'Use list_cad_export_templates to see available names. ' +
                       'If omitted or not found, Revit default export options are used.'
        },
        fileFormat: {
          type:        'string',
          description: 'Output file format: "DWG" (default) or "DXF".',
          default:     'DWG'
        }
      },
      required: ['sheetId']
    }
  },

  // ── list_cad_export_templates ─────────────────────────────
  {
    name:        'list_cad_export_templates',
    description: 'Lists all named DWG export templates (ExportDWGSettings) stored in the ' +
                 'active Revit document. Use the returned template names with export_sheet_to_cad.',
    inputSchema: {
      type:       'object',
      properties: {},
      required:   []
    }
  }
];

module.exports = { TOOLS };

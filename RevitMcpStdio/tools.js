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

/** @type {import('./types').McpTool[]} */
const TOOLS = [
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

  // ── create_wall ───────────────────────────────────────────
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
    description: 'Export Revit sheets to individual PDF files using PDF24 virtual printer. ' +
                 'Sheets are identified by their Revit element ID — use get_elements with ' +
                 'category="Sheets" to discover IDs. ' +
                 'Each PDF is named after the sheet\'s SheetNumber. ' +
                 'Paper size is auto-detected from the title block dimensions.',
    inputSchema: {
      type:       'object',
      properties: {
        sheetIds: {
          type:        'array',
          items:       { type: 'string' },
          description: 'Revit element IDs of the sheets to print (e.g. ["123456","789012"]). ' +
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
      required: ['sheetIds']
    }
  }
];

module.exports = { TOOLS };

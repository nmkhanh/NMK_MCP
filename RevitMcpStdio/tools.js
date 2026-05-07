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
                 'category. Use the BuiltInCategory suffix as the category name: ' +
                 '"Walls", "Doors", "Windows", "Floors", "Columns", "Stairs", "Rooms".',
    inputSchema: {
      type:       'object',
      properties: {
        category: {
          type:        'string',
          description: 'Revit BuiltInCategory suffix, e.g. "Walls", "Doors", "Floors".'
        },
        includeParameters: {
          type:        'boolean',
          description: 'Include Revit element parameters in the response. Slow on large result sets. Default: false.',
          default:     false
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
    description: 'Print Revit sheets to PDF using a virtual PDF printer (PDF24 preferred; ' +
                 'falls back to any installed PDF printer). ' +
                 'Paper size is auto-detected per sheet from the title block dimensions ' +
                 '(A0–A4, Letter, Tabloid) and matched to the printer\'s paper size list. ' +
                 'Supports combining all sheets into one PDF or exporting each sheet separately.',
    inputSchema: {
      type:       'object',
      properties: {
        sheetNumbers: {
          type:        'array',
          items:       { type: 'string' },
          description: 'Sheet numbers to export (e.g. ["A-001","S-101"]). ' +
                       'Omit or pass an empty array to export ALL sheets in the document.'
        },
        outputFolder: {
          type:        'string',
          description: 'Absolute folder path where PDF file(s) will be saved. ' +
                       'Defaults to the document\'s own folder, or Desktop if the document is unsaved.'
        },
        outputFileName: {
          type:        'string',
          description: 'Base file name (without .pdf extension) for the combined PDF. ' +
                       'Ignored when combine=false. Defaults to the document title.'
        },
        combine: {
          type:        'boolean',
          description: 'true (default) = merge all sheets into one PDF. ' +
                       'false = one PDF file per sheet.',
          default:     true
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
      required: []
    }
  }
];

module.exports = { TOOLS };

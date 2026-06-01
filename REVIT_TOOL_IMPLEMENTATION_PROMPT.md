# Revit MCP Tool Implementation Prompt

## Current implementation status

Last updated by Codex on 2026-05-30:

- Total registered/fallback tools: 184, including the 7 original tools.
- New roadmap tools implemented: 177.
- Target Revit version: Revit 2026 (.NET 8, Nice3point Revit API 2026.0.4, add-in output copy path `C:\ProgramData\Autodesk\Revit\Addins\2026\RevitMcpAddin\`).
- Build status: `dotnet build RevitMcpAddin\RevitMcpAddin.csproj -p:SkipRevitDeploy=true` succeeds. Full deploy build compiles but cannot copy while Revit 2026 locks the add-in DLLs.
- STDIO fallback status: `node -c RevitMcpStdio\tools.js` succeeds.
- Completed groups: discovery/type lookup, level/grid, parameters, views/sheets/ViewSheetSet, generic CRUD/transform/batch, material/type/view-template/text/detail/model line, family placement wrappers, building objects, annotation/tag/dimension/filled region, MEP, schedules/export schedule, load/reload family, groups/assemblies, reload links/worksets/design-option assignment, rebar/reinforcement/fabric/coupler/quantity workflows, override-in-view graphics/filter/category workflows.
- Remaining implementation work in this prompt: none for the listed roadmap tools. Remaining validation work: runtime smoke tests inside Revit with real project files and family/link/workshared samples.

Mục tiêu: mở rộng `NMK_MCP` thành bộ MCP tool tổng quát cho Revit, ưu tiên tạo/sửa/truy vấn đối tượng và parameter theo cách có thể bảo trì. Tất cả tool mới phải bám đúng cấu trúc hiện tại của dự án: mỗi tool có handler riêng, service function riêng, model riêng khi cần, và đăng ký rõ ràng trong `App.cs`.

## 1. Tool hiện có, không tạo lại

Các tool sau đã tồn tại và phải bỏ qua khi triển khai tool mới:

1. `get_active_document`
2. `get_elements`
3. `create_wall`
4. `select_elements_by_ids`
5. `print_sheet_to_pdf`
6. `export_sheet_to_cad`
7. `list_cad_export_templates`

Nếu tool mới cần chức năng tương tự, hãy tái sử dụng service/helper hiện có thay vì tạo bản sao.

## 1.1. Trang thai trien khai hien tai

Tat ca phase trong roadmap da duoc trien khai vao code theo cau truc:

1. Models trong `RevitMcpAddin/Models`.
2. Service functions trong `RevitMcpAddin/Services/Functions`.
3. Handler/tool definition trong `RevitMcpAddin/Mcp/Handlers`; Phase 8 dung shared `RebarToolHandler` registry de tranh lap boilerplate nhung moi tool van co schema va service call rieng.
4. Dang ky trong `RevitMcpAddin/App.cs`.
5. Fallback schema trong `RevitMcpStdio/tools.js`.

Trang thai kiem tra moi nhat: build C# thanh cong va `tools.js` parse thanh cong. Can test runtime truc tiep trong Revit cho cac API phu thuoc file thuc te nhu family reload, link reload, workset, schedule field, tag/dimension reference.

## 2. Cấu trúc bắt buộc cho mỗi tool mới

Mỗi tool mới nên được viết theo mẫu sau:

1. Model request/response nếu input phức tạp:
   - `RevitMcpAddin/Models/<Domain>Models.cs`
   - Dùng `Newtonsoft.Json.JsonProperty`.

2. Service function:
   - `RevitMcpAddin/Services/Functions/F_<Domain>.cs`
   - Dùng `public sealed partial class RevitService`.
   - Mọi Revit API call phải đi qua `_queue.EnqueueAsync(...)`.
   - Mọi thay đổi document phải nằm trong `Transaction` hoặc `TransactionGroup`.

3. Handler:
   - `RevitMcpAddin/Mcp/Handlers/<ToolName>Handler.cs`
   - Implement `IToolHandler`.
   - Tool name dùng `snake_case`.
   - `GetDefinition()` phải có JSON schema rõ ràng.
   - `HandleAsync()` chỉ parse/validate input rồi gọi `RevitService`; không gọi Revit API trực tiếp.

4. Register:
   - Thêm `.Register(new <ToolName>Handler(RevitSvc))` trong `RevitMcpAddin/App.cs`.

5. Node STDIO fallback:
   - Thêm schema vào `RevitMcpStdio/tools.js`.
   - Fallback này dùng khi Revit offline; live schema từ C# vẫn là nguồn chính khi Revit connected.

6. Kiểm tra:
   - Build C# project sau mỗi phase.
   - Test `tools/list` qua STDIO.
   - Nếu có Revit mở, test từng tool bằng request nhỏ trước khi chạy batch.

## 3. Quy ước thiết kế chung

- Input ID nên chấp nhận string hoặc integer nếu liên quan `ElementId`.
- Tọa độ dùng Revit internal unit là feet, trừ khi tool ghi rõ `meters` hoặc `millimeters`.
- Output luôn có:
  - `success`
  - `message`
  - `elementId` hoặc danh sách `elementIds` khi có
  - dữ liệu chính dạng object JSON.
- Tool sửa model phải trả về lỗi rõ ràng nếu:
  - không có active document
  - element/type/level/sheet/view không tồn tại
  - parameter read-only
  - unit/value không hợp lệ
  - transaction fail.
- Không làm tool quá “ma thuật”. Với thao tác nguy hiểm như delete/batch edit, yêu cầu input cụ thể.
- Tool batch phải có giới hạn số lượng mặc định và trả report từng item.

## 4. Phase triển khai tối ưu

Không triển khai toàn bộ danh sách một lượt. Mỗi phase nên đủ nhỏ để build/test xong trong một lượt làm việc, tránh chạm giới hạn context hoặc sửa quá nhiều file cùng lúc.

### Phase 0 - Nền tảng helper và chuẩn hóa

Mục tiêu: tạo nền móng dùng chung để các phase sau không lặp code.

Tool mới: không bắt buộc.

Việc cần làm:
1. Tạo helper parse `ElementId`.
2. Tạo helper resolve element/type/category/level/view/sheet theo id hoặc name.
3. Tạo helper đọc parameter:
   - instance parameter
   - type parameter
   - shared parameter
   - built-in parameter theo display name.
4. Tạo helper set parameter theo storage type:
   - string
   - integer
   - double
   - ElementId
   - yes/no boolean.
5. Tạo helper transaction/result formatting.
6. Ghi rõ giới hạn batch mặc định.

Lý do phase này đứng đầu: các tool create/edit/parameter đều cần chung các hàm này.

### Phase 1 - Level, Grid, Parameter cơ bản

Mục tiêu: bổ sung Level/Grid vì hiện chưa có `create_level` và `create_grid`, đồng thời mở khóa sửa parameter tổng quát.

Tool cần tạo:

1. `get_levels`
   - Lấy id, name, elevation, project/base info nếu có.

2. `create_level`
   - Input: `name`, `elevation`, `unit`.
   - Output: level id/name/elevation.

3. `update_level`
   - Sửa name/elevation.
   - Không tạo lại nếu chỉ cần edit.

4. `get_grids`
   - Lấy id, name, curve type, endpoint/arc data.

5. `create_grid`
   - Hỗ trợ line grid trước.
   - Input: `name`, `startX`, `startY`, `endX`, `endY`, optional `z`.
   - Có thể mở rộng arc grid sau.

6. `update_grid`
   - Sửa name và curve/extents nếu Revit API cho phép an toàn.

7. `get_element_parameters`
   - Lấy đầy đủ parameter của element.
   - Có option `includeTypeParameters`.

8. `set_element_parameter`
   - Set một parameter.
   - Validate read-only/storage type.

9. `set_element_parameters`
   - Set nhiều parameter trên một element.
   - Report từng parameter.

10. `batch_set_parameters`
   - Set parameter cho nhiều element.
   - Có `dryRun` và `maxItems`.

### Phase 2 - View, Sheet, ViewSheetSet

Mục tiêu: đáp ứng yêu cầu get/tạo mới/thêm sheet và view vào `ViewSheetSet`, đồng thời bổ sung tool đặt view lên sheet.

Tool cần tạo:

1. `get_views`
   - Lấy view id, name, view type, template flag, level id, scale.

2. `create_view`
   - Tạo FloorPlan/CeilingPlan/StructuralPlan/ThreeD/DraftingView tùy input.
   - Với plan view cần level.

3. `duplicate_view`
   - Duplicate mode: `Duplicate`, `WithDetailing`, `AsDependent`.

4. `update_view`
   - Sửa name, scale, detail level, discipline, view template nếu hợp lệ.

5. `get_sheets`
   - Lấy sheet id, number, name, title block, placed views.

6. `create_sheet`
   - Input: `sheetNumber`, `sheetName`, `titleBlockTypeId` hoặc `titleBlockFamilyName`.

7. `update_sheet`
   - Sửa sheet number/name và parameter sheet.

8. `place_view_on_sheet`
   - Đặt một view lên sheet bằng `Viewport.Create`.
   - Input: `sheetId`, `viewId`, `x`, `y`, `unit`.

9. `place_title_view_on_sheet`
   - Tool được thêm theo yêu cầu.
   - Mục đích: đặt một title/drafting/legend view hoặc view dùng làm title graphic lên sheet tại tọa độ xác định.
   - Input đề xuất:
     - `sheetId`
     - `viewId`
     - `x`
     - `y`
     - `unit`
     - optional `viewportTypeId`
     - optional `titleText` nếu workflow cần tạo/update text title liên quan.
   - Ghi chú triển khai:
     - Revit không có khái niệm universal "title view" độc lập cho mọi project. Cần định nghĩa tool này là wrapper chuyên dụng quanh `Viewport.Create`, ưu tiên đặt `DraftingView` hoặc `Legend` lên sheet.
     - Nếu view không thể đặt lên sheet, trả lỗi rõ: view đã được đặt, view type không hỗ trợ, hoặc view/template không hợp lệ.

10. `remove_view_from_sheet`
   - Xóa viewport khỏi sheet theo viewport id hoặc sheetId + viewId.

11. `get_view_sheet_sets`
   - Lấy ViewSheetSet hiện có trong PrintManager.

12. `create_view_sheet_set`
   - Tạo print set mới từ danh sách `viewIds` và `sheetIds`.

13. `add_views_to_view_sheet_set`
   - Thêm view vào set hiện có.

14. `add_sheets_to_view_sheet_set`
   - Thêm sheet vào set hiện có.

15. `remove_from_view_sheet_set`
   - Gỡ view/sheet khỏi set.

16. `delete_view_sheet_set`
   - Xóa ViewSheetSet theo tên.

Phase 2 có thể chia nhỏ thành 2A và 2B nếu context dài:
- 2A: `get_views`, `create_view`, `duplicate_view`, `update_view`, `get_sheets`, `create_sheet`, `place_view_on_sheet`, `place_title_view_on_sheet`.
- 2B: toàn bộ `ViewSheetSet`.

### Phase 3 - Generic CRUD và Transform

Mục tiêu: tạo bộ công cụ tổng quát cho hầu hết FamilyInstance và thao tác biến đổi.

Tool cần tạo:

1. `get_element`
2. `get_element_location`
3. `get_element_geometry_summary`
4. `create_family_instance`
5. `change_element_type`
6. `update_element`
7. `delete_elements`
8. `copy_elements`
9. `move_elements`
10. `rotate_elements`
11. `mirror_elements`
12. `pin_elements`
13. `unpin_elements`
14. `hide_elements_in_view`
15. `unhide_elements_in_view`
16. `apply_batch_operations`
17. `validate_batch_operations`

Ghi chú:
- `create_family_instance` phải hỗ trợ host/level/work plane khi cần.
- `apply_batch_operations` không nên là tool đầu tiên của phase; chỉ thêm sau khi các tool đơn ổn định.

### Phase 4 - Building objects chuyên biệt

Mục tiêu: tạo/sửa các đối tượng kiến trúc và structure phổ biến. Bỏ qua `create_wall` vì đã có.

Tool cần tạo:

1. `update_wall`
2. `create_floor`
3. `update_floor`
4. `create_ceiling`
5. `update_ceiling`
6. `create_roof`
7. `update_roof`
8. `create_opening`
9. `update_opening`
10. `create_room`
11. `update_room`
12. `create_area`
13. `update_area`
14. `create_space`
15. `update_space`
16. `place_room_tag`
17. `place_area_tag`
18. `place_space_tag`
19. `place_door`
20. `place_window`
21. `place_column`
22. `place_structural_framing`
23. `place_furniture`
24. `place_equipment`

Phase này nên chia 4A/4B/4C:
- 4A: floor/ceiling/roof/opening.
- 4B: room/area/space/tag.
- 4C: door/window/column/framing/furniture/equipment.

### Phase 5 - Annotation, Detail, Material, Type

Mục tiêu: thao tác annotation và thông tin trình bày.

Tool cần tạo:

1. `create_text_note`
2. `update_text_note`
3. `create_dimension`
4. `create_tag`
5. `create_detail_line`
6. `create_model_line`
7. `create_filled_region`
8. `create_material`
9. `update_material`
10. `duplicate_element_type`
11. `rename_element_type`
12. `set_type_parameter`
13. `set_type_parameters`
14. `apply_view_template`
15. `create_view_template`

### Phase 6 - MEP

Mục tiêu: thao tác MEP cơ bản nhưng không làm quá rộng ngay từ đầu.

Tool cần tạo:

1. `get_connectors`
2. `create_pipe`
3. `update_pipe`
4. `create_duct`
5. `update_duct`
6. `create_conduit`
7. `update_conduit`
8. `create_cable_tray`
9. `update_cable_tray`
10. `connect_mep_elements`
11. `disconnect_mep_elements`
12. `place_mep_fixture`
13. `place_mechanical_equipment`
14. `place_electrical_equipment`

Phase này nên chia theo discipline:
- 6A: connector + pipe.
- 6B: duct.
- 6C: conduit/cable tray/equipment.

### Phase 7 - Schedule, Export, Family, Advanced

Mục tiêu: hoàn thiện workflow quản lý dữ liệu và family.

Tool cần tạo:

1. `create_schedule`
2. `update_schedule`
3. `get_schedule_data`
4. `export_schedule`
5. `load_family`
6. `reload_family`
7. `get_family_symbols`
8. `activate_family_symbol`
9. `create_group`
10. `update_group`
11. `create_assembly`
12. `update_assembly`
13. `get_revit_links`
14. `reload_revit_link`
15. `manage_worksets`
16. `get_design_options`
17. `set_element_design_option`

### Phase 8 - Rebar, reinforcement, coupler

Muc tieu: bo sung nhom tool tao/doc/sua cot thep, fabric reinforcement, annotation, quantity, wrapper workflow cho cau kien pho bien, va coupler. Rebar API phu thuoc manh vao host, shape, cover, constraint, view, va family/type trong file Revit thuc te, nen cac tool tao phuc tap can smoke test truc tiep trong Revit 2026 voi mau column/beam/wall/slab.

Status: da trien khai trong Revit 2026 add-in va STDIO fallback; can smoke test runtime voi file Revit thuc te de xac nhan host/type/family/constraint rieng cua tung project.

#### Phase 8A - Rebar discovery, type, host, cover

1. `get_rebars`
2. `get_rebar_host_candidates`
3. `get_rebar_bar_types`
4. `get_rebar_shapes`
5. `get_rebar_hook_types`
6. `get_rebar_cover_types`
7. `get_rebar_constraints`
8. `get_rebar_centerline_curves`

#### Phase 8B - Rebar create/update co ban

9. `create_rebar_from_curves`
10. `create_rebar_from_shape`
11. `update_rebar_layout`
12. `update_rebar_hooks`
13. `update_rebar_constraints`
14. `set_rebar_cover`
15. `set_rebar_visibility_in_view`
16. `delete_rebars`

#### Phase 8C - Area, path, fabric reinforcement

17. `create_area_reinforcement`
18. `update_area_reinforcement`
19. `create_path_reinforcement`
20. `update_path_reinforcement`
21. `create_fabric_area`
22. `update_fabric_area`
23. `place_fabric_sheet`
24. `update_fabric_sheet`

#### Phase 8D - Rebar coupler

25. `get_rebar_coupler_types`
26. `get_rebar_couplers`
27. `get_rebar_coupler`
28. `create_rebar_coupler`
29. `update_rebar_coupler`
30. `change_rebar_coupler_type`
31. `delete_rebar_couplers`
32. `get_rebar_end_treatments`
33. `set_rebar_end_treatment`
34. `validate_rebar_coupler_placement`

#### Phase 8E - Rebar annotation, schedule, quantity

35. `create_rebar_tag`
36. `create_multi_rebar_annotation`
37. `create_rebar_schedule`
38. `get_rebar_quantities`
39. `set_rebar_partition`

#### Phase 8F - Workflow wrapper cho cau kien pho bien

40. `create_column_vertical_rebars`
41. `create_column_ties`
42. `create_beam_longitudinal_rebars`
43. `create_beam_stirrups`
44. `create_wall_rebar_grid`
45. `create_slab_rebar_grid`

## 5. Danh sách tool mới sau khi bỏ 7 tool hiện có

### Core discovery và parameter

1. `get_project_info`
2. `list_categories`
3. `list_element_types`
4. `list_family_types`
5. `get_element`
6. `get_element_location`
7. `get_element_geometry_summary`
8. `get_selected_elements`
9. `get_element_parameters`
10. `set_element_parameter`
11. `set_element_parameters`
12. `batch_set_parameters`
13. `set_type_parameter`
14. `set_type_parameters`
15. `find_elements_by_parameter`

### Level và Grid

16. `get_levels`
17. `create_level`
18. `update_level`
19. `get_grids`
20. `create_grid`
21. `update_grid`

### View, Sheet, ViewSheetSet

22. `get_views`
23. `create_view`
24. `duplicate_view`
25. `update_view`
26. `get_sheets`
27. `create_sheet`
28. `update_sheet`
29. `place_view_on_sheet`
30. `place_title_view_on_sheet`
31. `remove_view_from_sheet`
32. `get_view_sheet_sets`
33. `create_view_sheet_set`
34. `add_views_to_view_sheet_set`
35. `add_sheets_to_view_sheet_set`
36. `remove_from_view_sheet_set`
37. `delete_view_sheet_set`

### Generic CRUD và transform

38. `create_family_instance`
39. `change_element_type`
40. `update_element`
41. `delete_elements`
42. `copy_elements`
43. `move_elements`
44. `rotate_elements`
45. `mirror_elements`
46. `pin_elements`
47. `unpin_elements`
48. `hide_elements_in_view`
49. `unhide_elements_in_view`
50. `apply_batch_operations`
51. `validate_batch_operations`

### Building objects

52. `update_wall`
53. `create_floor`
54. `update_floor`
55. `create_ceiling`
56. `update_ceiling`
57. `create_roof`
58. `update_roof`
59. `create_opening`
60. `update_opening`
61. `create_room`
62. `update_room`
63. `create_area`
64. `update_area`
65. `create_space`
66. `update_space`
67. `place_room_tag`
68. `place_area_tag`
69. `place_space_tag`
70. `place_door`
71. `place_window`
72. `place_column`
73. `place_structural_framing`
74. `place_furniture`
75. `place_equipment`

### Annotation, detail, material, type

76. `create_text_note`
77. `update_text_note`
78. `create_dimension`
79. `create_tag`
80. `create_detail_line`
81. `create_model_line`
82. `create_filled_region`
83. `create_material`
84. `update_material`
85. `duplicate_element_type`
86. `rename_element_type`
87. `apply_view_template`
88. `create_view_template`

### MEP

89. `get_connectors`
90. `create_pipe`
91. `update_pipe`
92. `create_duct`
93. `update_duct`
94. `create_conduit`
95. `update_conduit`
96. `create_cable_tray`
97. `update_cable_tray`
98. `connect_mep_elements`
99. `disconnect_mep_elements`
100. `place_mep_fixture`
101. `place_mechanical_equipment`
102. `place_electrical_equipment`

### Schedule, family, advanced

103. `create_schedule`
104. `update_schedule`
105. `get_schedule_data`
106. `export_schedule`
107. `load_family`
108. `reload_family`
109. `get_family_symbols`
110. `activate_family_symbol`
111. `create_group`
112. `update_group`
113. `create_assembly`
114. `update_assembly`
115. `get_revit_links`
116. `reload_revit_link`
117. `manage_worksets`
118. `get_design_options`
119. `set_element_design_option`

### Rebar, reinforcement, coupler

120. `get_rebars`
121. `get_rebar_host_candidates`
122. `get_rebar_bar_types`
123. `get_rebar_shapes`
124. `get_rebar_hook_types`
125. `get_rebar_cover_types`
126. `get_rebar_constraints`
127. `get_rebar_centerline_curves`
128. `create_rebar_from_curves`
129. `create_rebar_from_shape`
130. `update_rebar_layout`
131. `update_rebar_hooks`
132. `update_rebar_constraints`
133. `set_rebar_cover`
134. `set_rebar_visibility_in_view`
135. `delete_rebars`
136. `create_area_reinforcement`
137. `update_area_reinforcement`
138. `create_path_reinforcement`
139. `update_path_reinforcement`
140. `create_fabric_area`
141. `update_fabric_area`
142. `place_fabric_sheet`
143. `update_fabric_sheet`
144. `get_rebar_coupler_types`
145. `get_rebar_couplers`
146. `get_rebar_coupler`
147. `create_rebar_coupler`
148. `update_rebar_coupler`
149. `change_rebar_coupler_type`
150. `delete_rebar_couplers`
151. `get_rebar_end_treatments`
152. `set_rebar_end_treatment`
153. `validate_rebar_coupler_placement`
154. `create_rebar_tag`
155. `create_multi_rebar_annotation`
156. `create_rebar_schedule`
157. `get_rebar_quantities`
158. `set_rebar_partition`
159. `create_column_vertical_rebars`
160. `create_column_ties`
161. `create_beam_longitudinal_rebars`
162. `create_beam_stirrups`
163. `create_wall_rebar_grid`
164. `create_slab_rebar_grid`
165. `get_view_overrides`
166. `set_element_overrides_in_view`
167. `clear_element_overrides_in_view`
168. `set_category_overrides_in_view`
169. `clear_category_overrides_in_view`
170. `set_category_visibility_in_view`
171. `set_filter_overrides_in_view`
172. `clear_filter_overrides_in_view`
173. `add_filter_to_view`
174. `remove_filter_from_view`
175. `set_view_detail_graphics`
176. `create_view_graphics_override_preset`
177. `apply_view_graphics_override_preset`

## 6. Thứ tự triển khai đề xuất thực tế

Thứ tự ưu tiên để có giá trị sớm và giảm rủi ro:

1. Phase 0 helper.
2. Phase 1 Level/Grid/Parameter.
3. Phase 2A View/Sheet/place view/title view.
4. Phase 2B ViewSheetSet.
5. Phase 3 generic CRUD/transform.
6. Phase 4A building shell.
7. Phase 4B room/area/space/tag.
8. Phase 4C family placement.
9. Phase 5 annotation/material/type.
10. Phase 6 MEP chia nhỏ.
11. Phase 7 advanced.
12. Phase 8 Rebar/Reinforcement/Coupler chia nho theo 8A-8F.
13. Phase 9 Override in View graphics/filter/category toolset.

Mỗi lần triển khai chỉ nên làm tối đa 8-12 tool, hoặc ít hơn nếu tool đụng nhiều Revit API phức tạp. Sau mỗi phase phải build/test trước khi sang phase tiếp theo.

## 7. Prompt cho lượt triển khai tiếp theo

Dùng prompt này khi bắt đầu từng phase:

```text
Triển khai Phase <N> trong `NMK_MCP` theo `REVIT_TOOL_IMPLEMENTATION_PROMPT.md`.

Yêu cầu:
- Không tạo lại 7 tool hiện có.
- Mỗi tool mới phải có Handler riêng trong `Mcp/Handlers`.
- Logic Revit API nằm trong `Services/Functions/F_<Domain>.cs`.
- Tạo/extend Models nếu input/output phức tạp.
- Register tool trong `App.cs`.
- Thêm fallback schema vào `RevitMcpStdio/tools.js`.
- Build/test sau khi sửa.
- Không gọi Revit API trực tiếp trong Handler.
- Dùng Transaction cho mọi thao tác sửa document.

Phase cần triển khai:
<liệt kê tool phase ở đây>
```

## 8. Ghi chú rủi ro hiện tại

1. Project/tài liệu có dấu hiệu lệch Revit 2025/2026:
   - README nói Revit 2026.
   - `RevitMcpAddin.csproj` đang reference/copy vào Revit 2025.
   - Trước khi build thật, cần xác nhận target Revit đang dùng.

2. `ViewSheetSet` trong Revit API phụ thuộc `PrintManager` và `ViewSet`; cần test trực tiếp trong Revit vì hành vi lưu set có thể khác theo version.

3. `place_title_view_on_sheet` cần thống nhất nghiệp vụ:
   - Nếu "title view" là Drafting View/Legend dùng làm title block phụ, triển khai bằng `Viewport.Create`.
   - Nếu ý là title block family, nên dùng `create_sheet` hoặc tool riêng `change_sheet_title_block`.

4. Các tool MEP và geometry phức tạp nên làm sau khi helper ID/type/parameter ổn định.

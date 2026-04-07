3D EXPORT VIEWS - USER GUIDE
=============================

WHAT THIS TOOL DOES
-------------------
This Revit add-in lets you quickly create 3D views from your floor plans
and ceiling plans. Instead of manually creating a 3D view, orienting it,
adding a section box, and applying a template for each floor - this tool
does it all at once for as many floors as you select.

Each 3D view it creates:
  - Is oriented like Revit's standard default 3D view (SE isometric angle)
  - Has a section box that matches the floor plan's boundaries and height range
  - Has your chosen view template applied
  - Is named automatically: "3D - DISCIPLINE - Level Name"
    (e.g., "3D - MECH - Level 1", "3D - ELEC - Roof")


HOW TO USE
----------
1. Open a Revit project that has floor plans or ceiling plans.

2. Click the "3D Export Views" button on the ribbon.
   A window opens - Revit stays usable in the background.

3. In the window:

   a. SELECT VIEWS
      - Browse the list of floor plans and ceiling plans in the DataGrid
      - Use the search box to filter by name (case-insensitive)
      - Click the checkbox on any row to select/deselect it
      - Click the column header ("checkmark All") to toggle all visible rows
      - Shift+Click a checkbox to select a range from the last clicked row
      - When multiple rows are highlighted, clicking one checkbox
        toggles all highlighted rows together
      - The selection count ("X of Y selected") updates live

   b. REFRESH
      - Click the refresh button (circular arrow, next to search box)
        to reload plans and templates from the current document
      - Useful after adding new levels or views without reopening the tool

   c. CHOOSE A DISCIPLINE
      - Pick from the dropdown: MECH, ELEC, PLUM, or ARCH
      - Or type your own custom label (e.g., "FP" for fire protection)

   d. CHOOSE A VIEW TEMPLATE
      - Pick a 3D view template from the dropdown
      - Only 3D-compatible templates are listed
      - Or choose "<None>" to skip applying a template

4. Click "Create" to generate the 3D views.
   - A progress bar and counter ("3 / 49") show real-time progress
   - The Create button is disabled while processing

5. RESULTS appear at the bottom of the window:
   - A summary count shows "(X created, Y failed)"
   - Each created view is listed with a green checkmark
   - Failed views are listed with a red X and an error message
   - Double-click any successful result to jump to that view in Revit

6. The window stays open - you can make more selections and create
   additional views without reopening the tool.


NAMING
------
Views are named:  3D - [DISCIPLINE] - [Level Name]

Examples:
  3D - MECH - Level 1
  3D - MECH - Level 2
  3D - ELEC - Roof
  3D - PLUM - Basement

If a name already exists, a number is added:
  3D - MECH - Level 1 (2)


NOTES
-----
- Only floor plans and ceiling plans appear in the list (no drafting views,
  sections, elevations, etc.)

- Only 3D-compatible view templates appear in the template dropdown.

- The section box is based on the floor plan's crop region (horizontal
  extents) and view range (vertical extents: top and bottom clip planes).

- If a floor plan has no crop region active, the tool still works - it
  uses the view's default extents.

- All views created in one batch can be undone with a single Ctrl+Z in Revit.

- If one view fails to create (e.g., due to a corrupt plan), the rest of
  the batch continues. The failure is reported in the results list.

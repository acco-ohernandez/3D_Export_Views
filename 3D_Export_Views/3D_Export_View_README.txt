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
      - Browse the list of floor plans and ceiling plans
      - Use the search box to filter by name
      - Click to select one, or Ctrl+Click / Shift+Click for multiple
      - Use "Select All" / "Deselect All" buttons for quick selection

   b. CHOOSE A DISCIPLINE
      - Pick from the dropdown: MECH, ELEC, PLUM, or ARCH
      - Or type your own custom label (e.g., "FP" for fire protection)

   c. CHOOSE A VIEW TEMPLATE
      - Pick a 3D view template from the dropdown
      - Or choose "<None>" to skip applying a template

4. Click "Create" to generate the 3D views.

5. RESULTS appear at the bottom of the window:
   - Each created view is listed by name
   - Click any view name to jump to it in Revit
   - If a view failed to create, you will see an error message

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

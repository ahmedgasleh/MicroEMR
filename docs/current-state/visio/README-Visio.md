# Using the MicroEMR Visio dataset

`MicroEMR-Objects.csv` is the node table and `MicroEMR-Relationships.csv` is the edge table. `ObjectId` values are stable within this current-state snapshot; relationships reference them through `FromObjectId` and `ToObjectId`.

## Suggested Visio workflow

1. Open Visio Professional and create a **Basic Diagram** or **Data Visualizer** diagram.
2. Import `MicroEMR-Objects.csv` as the shape data source.
3. Use `ObjectId` as the unique key and `ObjectName` as displayed text.
4. Group or color shapes by `Layer` or `Project`; use `Feature` as a secondary container/filter.
5. Import/join `MicroEMR-Relationships.csv` using `FromObjectId` and `ToObjectId`.
6. Label connectors with `RelationshipType`; optionally show `Description` in Shape Data rather than on the canvas.

If the installed Visio edition cannot create connectors from two CSV tables directly, import the object CSV first, then use Power Query/Excel or a Visio Data Visualizer template to flatten each relationship with the corresponding object names. The CSV files remain the authoritative machine-readable graph; no `.vsdx` was generated because reliable direct generation was not available without adding tooling.

The companion `../MicroEMR-architecture.dot` and Mermaid diagrams provide alternate layouts and a reference for manually arranging the Visio shapes.

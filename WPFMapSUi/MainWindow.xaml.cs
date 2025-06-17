using Mapsui;
using Mapsui.Animations;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using Mapsui.Nts.Providers;
using Mapsui.Nts.Providers.Shapefile;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Wpf;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Mapsui.Widgets.Zoom;
using Microsoft.Win32;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Dbf;
using NetTopologySuite.IO.Esri.Dbf.Fields;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;
using NetTopologySuite.IO.Esri.Shapefiles.Writers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = Mapsui.Styles.Brush;
using Color = Mapsui.Styles.Color;
using Geometry = NetTopologySuite.Geometries.Geometry;
using IFeature = Mapsui.IFeature;
using Layer = Mapsui.Layers.Layer;
using Path = System.IO.Path;
using Pen = Mapsui.Styles.Pen;
using Point = NetTopologySuite.Geometries.Point;
using ShapeType = NetTopologySuite.IO.Esri.ShapeType;
using Style = Mapsui.Styles.Style;

namespace WPFMapSUi
{
    public partial class MainWindow : Window
    {
        #region Properties
        private bool _isControlPressed = false;
        private bool _isMouseDown = false;
        private IFeature _selectionBox = null!; // Initialize to null with null-forgiving operator
        private MPoint _dragStartPoint = new ();
        private MPoint _dragEndPoint = new ();
        private SelectionState _selectionState = new ();
        private bool _unsavedChanges = false;
        private string currentFilePath = string.Empty; // Initialize to an empty string
        private string currentFileName = string.Empty; // Initialize to an empty string
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            MapControl.Focusable = true;
            MapControl.Focus();
            InitializeMapControlEvents();
            MapControl.Map = new Mapsui.Map(); // Initialize empty map
        }

        #region Initialize Map Control Events
        private void InitializeMapControlEvents()
        {
            MapControl.MouseLeftButtonDown += MapControl_MouseLeftButtonDown2;
            MapControl.MouseMove += MapControl_MouseMove;
            MapControl.MouseLeftButtonUp += MapControl_MouseLeftButtonUp;
            MapControl.KeyDown += MapControl_KeyDown;
            MapControl.KeyUp += MapControl_KeyUp;
            MapControl.Focusable = true;
            MapControl.Focus();
        }
        #endregion

        #region Map Control Event Handlers
        private void MapControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _isControlPressed = true;
                MapControl.Cursor = Cursors.Cross;
                e.Handled = true; // Prevent other controls from handling this
            }
        }

        private void MapControl_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _isControlPressed = false;
                MapControl.Cursor = Cursors.Arrow;
                e.Handled = true; // Prevent other controls from handling this
            }
        }

        private void MapControl_MouseLeftButtonDown2(object sender, MouseButtonEventArgs e)
        {
            if (!_isControlPressed) return;

            var position = e.GetPosition(MapControl);
            var screenPosition = new MPoint(position.X, position.Y);

            // Get map info at clicked position with a reasonable tolerance
            int toleranceInPixels = 10; // Adjust this value as needed
            var mapInfo = MapControl.GetMapInfo(screenPosition, toleranceInPixels);

            if (mapInfo?.Feature == null)
            {
                Debug.WriteLine("No feature found at click position");
                return;
            }

            // Check if feature is already selected
            var existingFeature = _selectionState.Features.FirstOrDefault(f =>
                FeaturesAreEqual(f, mapInfo.Feature));

            if (existingFeature != null)
            {
                _selectionState.Features.Remove(existingFeature);
                Debug.WriteLine("Feature deselected");
            }
            else
            {
                _selectionState.Features.Add(mapInfo.Feature);
                Debug.WriteLine("Feature selected");
            }

            UpdateFeatureStyles();
            e.Handled = true;
        }

        private bool FeaturesAreEqual(IFeature feature1, IFeature feature2)
        {
            if (feature1 is GeometryFeature gf1 && feature2 is GeometryFeature gf2)
            {
                return gf1.Geometry.EqualsExact(gf2.Geometry);
            }
            return false;
        }


        private void MapControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isControlPressed)
            {
                e.Handled = false;
                return;
            }

            var position = e.GetPosition(MapControl);
            var worldPosition = MapControl.Map.Navigator.Viewport.ScreenToWorld(position.X, position.Y);

            Debug.WriteLine($"Screen: {position.X},{position.Y} -> World: {worldPosition.X},{worldPosition.Y}");
            Debug.WriteLine($"Viewport resolution: {MapControl.Map.Navigator.Viewport.Resolution}");
            Debug.WriteLine($"worldPosition : "+ worldPosition + " Position : " + position);

            e.Handled = true;
            _isMouseDown = true;
            _dragStartPoint = worldPosition;

            // For single click selection
            if (e.ClickCount == 1)
            {
                SelectSingleFeature2(worldPosition);
            }
            else // For box selection
            {
                _selectionBox = new GeometryFeature(new Polygon( new LinearRing(new[]
                {
                new Coordinate(worldPosition.X, worldPosition.Y),
                new Coordinate(worldPosition.X, worldPosition.Y),
                new Coordinate(worldPosition.X, worldPosition.Y),
                new Coordinate(worldPosition.X, worldPosition.Y),
                new Coordinate(worldPosition.X, worldPosition.Y)
                })))
                {
                    Styles = new List<IStyle>
                    {
                        new VectorStyle
                        {
                            Enabled = true,
                            Fill = new Brush(Color.FromArgb(80, 100, 150, 255)),
                            Outline = new Pen(Color.FromArgb(180, 0, 0, 255), 2),
                            Opacity = 1.0f
                        }
                    }
                };

                var selectionLayer = new Layer("SelectionBox")
                {
                    DataSource = new MemoryProvider(_selectionBox),
                    Style = null,
                    IsMapInfoLayer = true
                };

                MapControl.Map.Layers.Add(selectionLayer);
            }
        }

        private void MapControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || !_isControlPressed || _selectionBox == null) return;

            var position = e.GetPosition(MapControl);
            var worldPosition = MapControl.Map.Navigator.Viewport.ScreenToWorld(position.X, position.Y);

            var coordinates = new[]
            {
        new Coordinate(_dragStartPoint.X, _dragStartPoint.Y),
        new Coordinate(worldPosition.X, _dragStartPoint.Y),
        new Coordinate(worldPosition.X, worldPosition.Y),
        new Coordinate(_dragStartPoint.X, worldPosition.Y),
        new Coordinate(_dragStartPoint.X, _dragStartPoint.Y)
    };

            if (_selectionBox is GeometryFeature geometryFeature)
            {
                geometryFeature.Geometry = new Polygon(new LinearRing(coordinates));
                MapControl.Refresh();
            }
            e.Handled = true;
        }

        private void MapControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isMouseDown || !_isControlPressed) return;

            try
            {
                _isMouseDown = false;

                if (_selectionBox != null)
                {
                    var position = e.GetPosition(MapControl);
                    var worldEndPoint = MapControl.Map.Navigator.Viewport.ScreenToWorld(position.X, position.Y);

                    // Only do box selection if we've actually dragged a meaningful distance
                    if (_dragStartPoint.Distance(worldEndPoint) > MapControl.Map.Navigator.Viewport.Resolution * 5)
                    {
                        SelectFeaturesInBox(_dragStartPoint, worldEndPoint);
                    }

                    // Remove selection box layer
                    var selectionLayer = MapControl.Map.Layers.FirstOrDefault(l => l.Name == "SelectionBox");
                    if (selectionLayer != null)
                    {
                        MapControl.Map.Layers.Remove(selectionLayer);
                    }
                }
            }
            finally
            {
                e.Handled = true;
                MapControl.Refresh();
            }
        }
        #endregion

        #region Feature Selection and Highlighting
        private async void SelectFeaturesInBox(MPoint start, MPoint end)
        {
            var box = new Envelope(
                Math.Min(start.X, end.X),
                Math.Max(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.Y, end.Y));

            _selectionState.Features.Clear();

            var selectionPolygon = new Polygon(new LinearRing(new[]
            {
                new Coordinate(box.MinX, box.MinY),
                new Coordinate(box.MaxX, box.MinY),
                new Coordinate(box.MaxX, box.MaxY),
                new Coordinate(box.MinX, box.MaxY),
                new Coordinate(box.MinX, box.MinY)
            }));

            foreach (var layer in MapControl.Map.Layers.OfType<Layer>())
            {
                if (layer.DataSource == null || layer.Name == "SelectionBox") continue;

                var fetchInfo = new FetchInfo(
                    new MSection(new MRect(box.MinX, box.MinY, box.MaxX, box.MaxY),
                    MapControl.Map.Navigator.Viewport.Resolution),
                    ChangeType.Discrete.ToString());

                var features = layer.DataSource.GetFeaturesAsync(fetchInfo);
                foreach (var feature in await features)
                {
                    if (feature is GeometryFeature geometryFeature &&
                        geometryFeature.Geometry != null &&
                        geometryFeature.Geometry.Intersects(selectionPolygon))
                    {
                        _selectionState.Features.Add(feature);
                    }
                }
            }

            UpdateFeatureStyles();
        }


        private void SelectSingleFeature2(MPoint screenPosition)
        {
            // Get current resolution (map units per pixel)
            double resolution = MapControl.Map.Navigator.Viewport.Resolution;

            // Calculate dynamic tolerance - these values may need adjustment
            int toleranceInPixels = (int)Math.Max(5, 50 / resolution); // Minimum 5 pixels, scales with zoom
            double toleranceInMapUnits = toleranceInPixels * resolution;

            // Get map info with tolerance
            var mapInfo = MapControl.GetMapInfo(screenPosition, toleranceInPixels);

            Debug.WriteLine($"Selection at zoom: 1:{1 / resolution} with tolerance: {toleranceInPixels}px ({toleranceInMapUnits} map units)");

            if (mapInfo?.Feature == null)
            {
                Debug.WriteLine("No feature found at click position");
                return;
            }

            // Check if feature is already selected using more reliable comparison
            var existingFeature = _selectionState.Features.FirstOrDefault(f =>
                f is GeometryFeature gf1 &&
                mapInfo.Feature is GeometryFeature gf2 &&
                gf1.Geometry.EqualsTopologically(gf2.Geometry));

            if (existingFeature != null)
            {
                _selectionState.Features.Remove(existingFeature);
                Debug.WriteLine("Feature deselected");
            }
            else
            {
                _selectionState.Features.Add(mapInfo.Feature);
                Debug.WriteLine("Feature selected");
            }

            UpdateFeatureStyles();
        }
        private void SelectSingleFeature(MPoint worldPosition)
        {
            double resolution = MapControl.Map.Navigator.Viewport.Resolution;
            int tolerance = (int)(resolution * 5); 

            var info = MapControl.GetMapInfo(worldPosition,tolerance);


            // Add debug logging
            Debug.WriteLine($"MapInfo at {worldPosition}:");
            Debug.WriteLine($"Layer: {info?.Layer?.Name}");
            Debug.WriteLine($"Feature: {info?.Feature}");
            Debug.WriteLine($"All layers: {string.Join(", ", MapControl.Map.Layers.Select(l => l.Name))}");
            if (info?.Feature == null)
            {
                Debug.WriteLine("No feature found at click position");
                return;
            }

            Debug.WriteLine($"Feature found: {info.Feature.GetType().Name}");

            // Check if feature is already selected
            var existingIndex = _selectionState.Features.FindIndex(f =>
                f is GeometryFeature gf1 &&
                info.Feature is GeometryFeature gf2 &&
                gf1.Geometry.EqualsExact(gf2.Geometry));

            if (existingIndex >= 0)
            {
                _selectionState.Features.RemoveAt(existingIndex);
                Debug.WriteLine("Feature deselected");
            }
            else
            {
                _selectionState.Features.Add(info.Feature);
                Debug.WriteLine("Feature selected");
            }

            UpdateFeatureStyles();
        }

        private void UpdateFeatureStyles()
        {
            // Clear previous highlights
            var existingHighlightLayer = MapControl.Map.Layers.FirstOrDefault(l => l.Name == "SelectionHighlight");
            if (existingHighlightLayer != null)
            {
                MapControl.Map.Layers.Remove(existingHighlightLayer);
            }

            if (_selectionState.Features.Count > 0)
            {
                var highlightFeatures = new List<IFeature>();
                foreach (var feature in _selectionState.Features)
                {
                    if (feature is GeometryFeature geometryFeature)
                    {
                        var highlightFeature = new GeometryFeature(geometryFeature.Geometry.Copy())
                        {
                            Styles = new List<IStyle>
                    {
                        new VectorStyle
                        {
                            Fill = new Brush(Color.FromArgb(100, 255, 255, 0)),
                            Outline = new Pen(Color.Red, 2),
                            Opacity = 1.0f
                        }
                    }
                        };
                        highlightFeatures.Add(highlightFeature);
                    }
                }

                var newHighlightLayer = new Layer("SelectionHighlight")
                {
                    DataSource = new MemoryProvider(highlightFeatures),
                    Style = null,
                    IsMapInfoLayer = false
                };

                MapControl.Map.Layers.Insert(0, newHighlightLayer);
                btnClearSelection.Visibility = Visibility.Visible;
            }
            else
            {
                btnClearSelection.Visibility = Visibility.Collapsed;
            }

            UpdateAttributeGrid();
            MapControl.Refresh();
        }

        #endregion

        #region Attribute Grid and Multiple Editing
        private void UpdateAttributeGrid()
        {
            Debug.WriteLine($"Updating grid for {_selectionState.Features.Count} features");

            if (_selectionState.Features.Count == 1)
            {
                var feature = _selectionState.Features.First();
                Debug.WriteLine($"Single feature selected with {feature.Fields.Count()} fields");

                var attributes = feature.Fields
                    .Select(field => new FeatureAttribute
                    {
                        Key = field,
                        Value = feature[field]?.ToString() ?? string.Empty
                    })
                    .ToList();

                attributeGrid.ItemsSource = attributes;
                btnBatchEdit.Visibility = Visibility.Collapsed;
                btnSaveAttributes.IsEnabled = true;
            }
            else if (_selectionState.Features.Count > 1)
            {
                Debug.WriteLine($"Multiple features selected: {_selectionState.Features.Count}");
                attributeGrid.ItemsSource = null;
                btnBatchEdit.Visibility = Visibility.Visible;
                btnSaveAttributes.IsEnabled = false;
            }
            else
            {
                Debug.WriteLine("No features selected");
                attributeGrid.ItemsSource = null;
                btnBatchEdit.Visibility = Visibility.Collapsed;
                btnSaveAttributes.IsEnabled = false;
            }

            selectedLabel.Content = $"{_selectionState.Features.Count} elementos seleccionados";
            Debug.WriteLine($"Label updated: {selectedLabel.Content}");
        }

        private void SaveAttributes_Click(object sender, RoutedEventArgs e)
        {
            if (_selectionState.Features.Count != 1) return;

            var feature = _selectionState.Features.First();
            var attributes = attributeGrid.ItemsSource as IEnumerable<FeatureAttribute>;

            if (attributes != null)
            {
                foreach (var attr in attributes)
                {
                    if (feature.Fields.Contains(attr.Key))
                    {
                        var originalValue = feature[attr.Key];
                        object newValue = attr.Value;

                        if (originalValue != null)
                        {
                            try
                            {
                                if (originalValue is int)
                                    newValue = int.Parse(attr.Value);
                                else if (originalValue is double)
                                    newValue = double.Parse(attr.Value);
                                else if (originalValue is DateTime)
                                    newValue = DateTime.Parse(attr.Value);
                            }
                            catch { /* Keep as string if conversion fails */ }
                        }

                        feature[attr.Key] = newValue;
                    }
                }

                MarkChangesAsUnsaved();
                MessageBox.Show("Atributos guardados correctamente", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BatchEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectionState.Features.Count < 2) return;

            var batchWindow = new BatchEditWindow(_selectionState.Features)
            {
                Owner = this
            };

            if (batchWindow.ShowDialog() == true)
            {
                // Changes were applied
                MarkChangesAsUnsaved();
                MessageBox.Show("Cambios aplicados a los elementos seleccionados.", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh the map to show any style changes
                MapControl.Refresh();
            }
        }
        #endregion

        #region Clear Selection
        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            ClearAllSelections();
        }
        private void ClearAllSelections()
        {
            _selectionState.Features.Clear();

            // Elimina la caja de selección si existe
            var selectionBoxLayer = MapControl.Map.Layers.FirstOrDefault(l => l.Name == "SelectionBox");
            if (selectionBoxLayer != null)
            {
                MapControl.Map.Layers.Remove(selectionBoxLayer);
            }

            // Elimina el highlight si existe
            var highlightLayer = MapControl.Map.Layers.FirstOrDefault(l => l.Name == "SelectionHighlight");
            if (highlightLayer != null)
            {
                MapControl.Map.Layers.Remove(highlightLayer);
            }

            UpdateAttributeGrid();
            // Hide the clear button
            btnClearSelection.Visibility = Visibility.Collapsed;

            UpdateFeatureStyles();
            MapControl.Refresh();
        }
        #endregion

        #region File and Map & load shapefile
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar archivo shapefile (.shp)",
                Filter = "Shapefiles (*.shp)|*.shp",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                currentFilePath = dialog.FileName;
                currentFileName = Path.GetFileName(currentFilePath);
                var renderer = Mapsui.Rendering.RenderFormat.Png;
                LoadShapefile(currentFilePath, renderer);
                menuSaveAs.IsEnabled = true;
                limpiarMapa.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("No se seleccionó ningún archivo.", "Información",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadShapefile(string shapefilePath, Mapsui.Rendering.RenderFormat renderer)
        {
            InitializeMapControlEvents();

            // Initialize map properties
            MapControl.Map.BackColor = Mapsui.Styles.Color.White;
            MapControl.Map.CRS = "EPSG:3857";

            var shapeProvider = new ShapeFile(shapefilePath, true)
            {
                CRS = "EPSG:3857" 
            };

            // Create layer with proper interactive settings
            var vectorLayer = new Layer("Polygons")
            {
                DataSource = shapeProvider,
                Style = new VectorStyle
                {
                    Fill = new Brush(Color.FromArgb(200, 0, 255, 255)),  // More visible cyan
                    Outline = new Pen(Color.Black, 2),  // Thicker outline
                    Enabled = true,
                    Opacity = 1.0f,
                    MinVisible = 0,
                    MaxVisible = double.MaxValue
                },
                IsMapInfoLayer = true,  // CRUCIAL for feature selection
                Enabled = true
            };

            // Remove any existing layer first
            var existing = MapControl.Map.Layers.FirstOrDefault(l => l.Name == "Polygons");
            if (existing != null)
            {
                MapControl.Map.Layers.Remove(existing);
            }

            // Set zoom resolutions
            int steps = 30;
            double start = 20;
            double end = 0.00001;
            double[] resolutions = new double[steps];
            for (int i = 0; i < steps; i++)
            {
                resolutions[i] = start * Math.Pow(end / start, (double)i / (steps - 1));
            }
            MapControl.Map.Navigator.OverrideResolutions = resolutions;

            try
            {
                // Add debug information about the shapefile
                var extent = shapeProvider.GetExtent();
                Debug.WriteLine($"Shapefile extent: {extent.MinX},{extent.MinY} to {extent.MaxX},{extent.MaxY}");
                Debug.WriteLine($"Feature count: {shapeProvider.GetFeatureCount()}");

                // Add the layer
                MapControl.Map.Layers.Add(vectorLayer);

                // Set map bounds
                //MapControl.Map.Navigator.OverridePanBounds = extent;
                MapControl.Map.Home = n => n.ZoomToBox(extent, MBoxFit.Fit);

                // Add widgets
                MapControl.Map.Widgets.Add(CreateScaleBar(MapControl.Map));
                MapControl.Map.Widgets.Add(new ZoomInOutWidget { MarginX = 20, MarginY = 20 });

                // Fetch features using FetchInfo
                var fetchInfo = new FetchInfo(
                    new MSection(extent, MapControl.Map.Navigator.Viewport.Resolution),
                    ChangeType.Discrete.ToString());

                var firstFeature = shapeProvider.GetFeaturesAsync(fetchInfo).Result.Cast<GeometryFeature>().FirstOrDefault();
                if (firstFeature != null)
                {
                    Debug.WriteLine($"First feature at: {firstFeature.Geometry.Coordinate}");
                }

                MessageBox.Show($"Loaded {shapeProvider.GetFeatureCount()} features");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading shapefile: {ex.Message}");
                MessageBox.Show("The file is corrupt or empty, please select a valid shapefile");
            }
        }
        #endregion

        #region Save Shapefile As
        private async void SaveShapefileAsAsync(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Shapefiles (*.shp)|*.shp|All files (*.*)|*.*",
                Title = "Guardar shapefile",
                DefaultExt = "shp",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (saveDialog.ShowDialog() == true)
            {
                var progressDialog = new ProgressWindow("Guardando shapefile...");
                progressDialog.Show();

                try
                {
                    var shapefileLayer = MapControl.Map.Layers
                        .OfType<Layer>()
                        .FirstOrDefault(l => l.DataSource is ShapeFile);

                    if (shapefileLayer?.DataSource is not ShapeFile shapefile)
                    {
                        MessageBox.Show("No se encontró capa de shapefile", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        progressDialog.Close();
                        return;
                    }

                    var extent = shapefile.GetExtent();
                    if (extent == null)
                    {
                        MessageBox.Show("El shapefile no tiene un área válida para procesar.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        progressDialog.Close();
                        return;
                    }

                    var fetchInfo = new FetchInfo(
                        new MSection(extent, MapControl.Map.Navigator.Viewport.Resolution),
                        ChangeType.Discrete.ToString());

                    var fieldTypes = new Dictionary<string, Type>();
                    var sampleFeature = (await shapefile.GetFeaturesAsync(fetchInfo)).Cast<GeometryFeature>().FirstOrDefault();
                    if (sampleFeature != null)
                    {
                        foreach (var field in sampleFeature.Fields)
                        {
                            var value = sampleFeature[field];
                            fieldTypes[field] = value?.GetType() ?? typeof(string);
                        }
                    }

                    var dbfFields = new List<DbfField>();
                    foreach (var field in fieldTypes.Keys)
                    {
                        var fieldType = fieldTypes[field];
                        var cleanFieldName = CleanFieldName(field);

                        if (fieldType == typeof(DateTime))
                        {
                            dbfFields.Add(DbfField.Create(cleanFieldName, typeof(DateTime)));
                        }
                        else if (fieldType == typeof(decimal) || fieldType == typeof(double) || fieldType == typeof(float))
                        {
                            dbfFields.Add(DbfField.Create(cleanFieldName, typeof(double)));
                        }
                        else if (fieldType == typeof(int) || fieldType == typeof(short) || fieldType == typeof(long))
                        {
                            dbfFields.Add(DbfField.Create(cleanFieldName, typeof(int)));
                        }
                        else
                        {
                            dbfFields.Add(DbfField.Create(cleanFieldName, typeof(string)));
                        }
                    }

                    var options = new ShapefileWriterOptions(ShapeType.Polygon, dbfFields.ToArray());
                    using var writer = Shapefile.OpenWrite(Path.ChangeExtension(saveDialog.FileName, null), options);

                    var features = await shapefile.GetFeaturesAsync(fetchInfo);
                    var totalFeatures = features.Count();
                    var processedFeatures = 0;

                    foreach (var mapFeature in features.Cast<GeometryFeature>())
                    {
                        writer.Geometry = mapFeature.Geometry as Geometry;

                        foreach (var field in mapFeature.Fields)
                        {
                            var value = mapFeature[field];
                            var cleanFieldName = CleanFieldName(field);
                            var fieldIndex = dbfFields.FindIndex(f => f.Name.Equals(cleanFieldName, StringComparison.OrdinalIgnoreCase));

                            if (fieldIndex >= 0)
                            {
                                if (value == null || value is DBNull)
                                {
                                    writer.Fields[fieldIndex].Value = null;
                                    continue;
                                }

                                if (dbfFields[fieldIndex].FieldType == DbfType.Date && value is DateTime dateValue)
                                {
                                    writer.Fields[fieldIndex].Value = dateValue;
                                }
                                else if (dbfFields[fieldIndex].FieldType == DbfType.Numeric)
                                {
                                    writer.Fields[fieldIndex].Value = Convert.ToDouble(value);
                                }
                                else
                                {
                                    writer.Fields[fieldIndex].Value = value?.ToString();
                                }
                            }
                        }
                        writer.Write();

                        processedFeatures++;
                        progressDialog.UpdateProgress((double)processedFeatures / totalFeatures * 100);
                    }

                    _unsavedChanges = false;
                    menuSaveAs.IsEnabled = false;

                    progressDialog.Close();
                    MessageBox.Show($"Shapefile guardado exitosamente ({totalFeatures} elementos)", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    progressDialog.Close();
                    MessageBox.Show($"Error al guardar shapefile: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region Helper Methods

        private string CleanFieldName(string originalName)
        {
            var cleaned = Regex.Replace(originalName, "[^a-zA-Z0-9_]", "_");
            if (char.IsDigit(cleaned[0])) cleaned = "F_" + cleaned;
            return cleaned.Length > 10 ? cleaned.Substring(0, 10) : cleaned;
        }

        private void menuScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string screenshotsDir = Path.Combine(".\\", "Capturas");
                Directory.CreateDirectory(screenshotsDir);

                string fileName = $"Captura_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(screenshotsDir, fileName);

                RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                    (int)this.ActualWidth,
                    (int)this.ActualHeight,
                    96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderTarget.Render(this);

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                using (FileStream stream = new FileStream(fullPath, FileMode.Create))
                {
                    encoder.Save(stream);
                }

                MessageBox.Show($"Captura guardada en: {fullPath}", "Éxito",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al guardar captura: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static ScaleBarWidget CreateScaleBar(Map map) => new ScaleBarWidget(map)
        {
            TextAlignment = Mapsui.Widgets.Alignment.Center,
            HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Center,
            VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom,
        };

        #endregion

        #region Close Application unsavedChanges and clear map
        private void CloseApp(object sender, RoutedEventArgs e)
        {
            if (_unsavedChanges)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Tienes cambios sin guardar. ¿Estás seguro que quieres salir?",
                    "Cambios no guardados",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                    return;
            }
            Application.Current.Shutdown();
        }

        private void MarkChangesAsUnsaved()
        {
            _unsavedChanges = true;
            menuSaveAs.IsEnabled = true;
        }

        private void mapViewClear(object sender, RoutedEventArgs e)
        {
            MapControl.Map.Layers.Clear();
            menuSaveAs.IsEnabled = false;
            limpiarMapa.IsEnabled = false;
            _selectionState.Features.Clear();
            ClearAllSelections();
            UpdateAttributeGrid();
            MapControl.Map.Refresh();
        }
        #endregion
        
    }
}
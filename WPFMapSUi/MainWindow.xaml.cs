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
            MapControl.GotKeyboardFocus += MapControl_GotKeyboardFocus;
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
            if (feature1 == feature2) return true;
            if (feature1 is not GeometryFeature gf1 || feature2 is not GeometryFeature gf2)
                return false;
            if (gf1.Geometry == null || gf2.Geometry == null)
                return false;

            return gf1.Geometry.EqualsExact(gf2.Geometry);
        }
        private void MapControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || !_isControlPressed || _selectionBox == null) return;

            var position = e.GetPosition(MapControl);
            var worldPosition = MapControl.Map.Navigator.Viewport.ScreenToWorld(position.X, position.Y);

            // Create coordinates that work in all directions
            var minX = Math.Min(_dragStartPoint.X, worldPosition.X);
            var maxX = Math.Max(_dragStartPoint.X, worldPosition.X);
            var minY = Math.Min(_dragStartPoint.Y, worldPosition.Y);
            var maxY = Math.Max(_dragStartPoint.Y, worldPosition.Y);

            var coordinates = new[]
            {
        new Coordinate(minX, minY),
        new Coordinate(maxX, minY),
        new Coordinate(maxX, maxY),
        new Coordinate(minX, maxY),
        new Coordinate(minX, minY)
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

                    
                        SelectFeaturesInBox(_dragStartPoint, worldEndPoint);

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
            // Create envelope with correct min/max values
            var box = new Envelope(
                Math.Min(start.X, end.X),
                Math.Max(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.Y, end.Y));

            Debug.WriteLine($"Selection box: {box}");

            _selectionState.Features.Clear();

            foreach (var layer in MapControl.Map.Layers.OfType<Layer>())
            {
                if (layer.DataSource == null || layer.Name == "SelectionBox") continue;

                // Create MRect with correct coordinates
                var rect = new MRect(box.MinX, box.MinY, box.MaxX, box.MaxY);
                Debug.WriteLine($"Fetching features in rect: {rect}");

                var fetchInfo = new FetchInfo(
                    new MSection(rect, MapControl.Map.Navigator.Viewport.Resolution),
                    ChangeType.Discrete.ToString());

                var features = await layer.DataSource.GetFeaturesAsync(fetchInfo);
                Debug.WriteLine($"Found {features.Count()} features in bounding box");

                foreach (var feature in features)
                {
                    if (feature is GeometryFeature geometryFeature &&
                        geometryFeature.Geometry != null &&
                        geometryFeature.Geometry.EnvelopeInternal.Intersects(box))
                    {
                        Debug.WriteLine($"Adding feature: {geometryFeature}");
                        _selectionState.Features.Add(feature);
                    }
                }
            }

            Debug.WriteLine($"Total features selected: {_selectionState.Features.Count}");
            UpdateFeatureStyles();
        }


        private void SelectSingleFeature2(MPoint screenPosition)
        {
            Debug.WriteLine($"Selection at: {screenPosition}");

            // Get map info with tolerance
            int toleranceInPixels = 10;
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
            Debug.WriteLine($"Total selected: {_selectionState.Features.Count}");
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

            // Create a new list with cloned features
            var featuresCopy = new List<IFeature>();
            foreach (var feature in _selectionState.Features)
            {
                if (feature is GeometryFeature gf)
                {
                    var clone = new GeometryFeature(gf.Geometry.Copy());

                    // Copy attributes
                    foreach (var field in gf.Fields)
                    {
                        clone[field] = gf[field];
                    }

                    featuresCopy.Add(clone);
                }
            }

            var batchWindow = new BatchEditWindow(featuresCopy)
            {
                Owner = this
            };

            if (batchWindow.ShowDialog() == true)
            {
                // Update the original features with modified values
                for (int i = 0; i < featuresCopy.Count; i++)
                {
                    var modifiedFeature = featuresCopy[i] as GeometryFeature;
                    var originalFeature = _selectionState.Features[i] as GeometryFeature;

                    if (modifiedFeature != null && originalFeature != null)
                    {
                        foreach (var field in modifiedFeature.Fields)
                        {
                            originalFeature[field] = modifiedFeature[field];
                        }
                    }
                }

                MarkChangesAsUnsaved();
                MessageBox.Show("Cambios aplicados a los elementos seleccionados.", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh the UI
                UpdateFeatureStyles();

                // Return focus to map control
                MapControl.Focus();
            }
        }
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            MapControl.Focus();
        }

        private void MapControl_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Ensure the MapControl stays focused
            MapControl.Focus();
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
                        progressDialog.Close();
                        return;
                    }

                    var extent = shapefile.GetExtent();
                    if (extent == null)
                    {
                        progressDialog.Close();
                        return;
                    }

                    var fetchInfo = new FetchInfo(
                        new MSection(extent, MapControl.Map.Navigator.Viewport.Resolution),
                        ChangeType.Discrete.ToString());

                    // Get a sample feature to determine field types
                    var sampleFeature = (await shapefile.GetFeaturesAsync(fetchInfo)).Cast<GeometryFeature>().FirstOrDefault();
                    if (sampleFeature == null)
                    {
                        progressDialog.Close();
                        return;
                    }

                    // Create DBF fields based on the sample feature
                    var dbfFields = CreateDbfFieldsFromSampleFeature(sampleFeature).ToList(); // Convert to List to use FindIndex
                    if (dbfFields.Count == 0)
                    {
                        progressDialog.Close();
                        MessageBox.Show("No se encontraron campos para guardar en el shapefile.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
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
                            var fieldIndex = dbfFields.FindIndex(f => f.Name.Equals(cleanFieldName, StringComparison.OrdinalIgnoreCase)); // Fix: Convert dbfFields to List

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

        private void EnsureMapControlFocus()
        {
            if (!MapControl.IsFocused)
            {
                MapControl.Focus();
            }
        }
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
        private IEnumerable<DbfField> CreateDbfFieldsFromSampleFeature(IFeature sampleFeature)
        {
            var dbfFields = new List<DbfField>();

            foreach (var field in sampleFeature.Fields)
            {
                var value = sampleFeature[field];
                var cleanFieldName = CleanFieldName(field);

                // Use DbfField.Create to create instances of DbfField
                if (value is DateTime)
                {
                    dbfFields.Add(DbfField.Create(cleanFieldName, typeof(DateTime)));
                }
                else if (value is int || value is short || value is long)
                {
                    dbfFields.Add(DbfField.Create(cleanFieldName, typeof(int)));
                }
                else if (value is double || value is float || value is decimal)
                {
                    dbfFields.Add(DbfField.Create(cleanFieldName, typeof(double)));
                }
                else
                {
                    dbfFields.Add(DbfField.Create(cleanFieldName, typeof(string)));
                }
            }

            return dbfFields;
        }
        #endregion
        
    }
}
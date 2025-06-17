using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Mapsui;
using Mapsui.Nts;

namespace WPFMapSUi
{
    public partial class BatchEditWindow : Window
    {
        public List<IFeature> SelectedFeatures { get; }
        public int SelectedFeaturesCount => SelectedFeatures.Count;

        public BatchEditWindow(List<IFeature> selectedFeatures)
        {
            InitializeComponent();
            SelectedFeatures = selectedFeatures;
            DataContext = this;
            LoadAttributes();
        }

        private void LoadAttributes()
        {
            if (SelectedFeatures.Count == 0) return;

            // Get all unique fields from all selected features
            var allFields = SelectedFeatures
                .SelectMany(f => f.Fields)
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            var attributes = new List<BatchEditAttribute>();

            foreach (var field in allFields)
            {
                // Get values for this field from all features
                var values = SelectedFeatures
                    .Where(f => f.Fields.Contains(field))
                    .Select(f => f[field])
                    .ToList();

                // Check if all values are the same
                var firstValue = values.FirstOrDefault();
                bool allSame = values.All(v => object.Equals(v, firstValue));

                attributes.Add(new BatchEditAttribute
                {
                    Key = field,
                    CurrentValue = allSame ? (firstValue?.ToString() ?? "") : "[Varios valores]",
                    NewValue = null,
                    ValueType = firstValue?.GetType() ?? typeof(string)
                });
            }

            batchAttributeGrid.ItemsSource = attributes;
            selectedCountText.Text = $"{SelectedFeaturesCount} elementos seleccionados";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var attributes = batchAttributeGrid.ItemsSource as IEnumerable<BatchEditAttribute>;
            if (attributes == null) return;

            foreach (var attr in attributes.Where(a => !string.IsNullOrEmpty(a.NewValue)))
            {
                foreach (var feature in SelectedFeatures.OfType<GeometryFeature>())
                {
                    if (feature.Fields.Contains(attr.Key))
                    {
                        try
                        {
                            // Convert value to appropriate type
                            object newValue = attr.NewValue;
                            if (attr.ValueType == typeof(int))
                                newValue = int.Parse(attr.NewValue);
                            else if (attr.ValueType == typeof(double))
                                newValue = double.Parse(attr.NewValue);
                            else if (attr.ValueType == typeof(DateTime))
                                newValue = DateTime.Parse(attr.NewValue);

                            feature[attr.Key] = newValue;
                        }
                        catch
                        {
                            // If conversion fails, keep as string
                            feature[attr.Key] = attr.NewValue;
                        }
                    }
                }
            }

            DialogResult = true;
            Close();
        }
    }
}


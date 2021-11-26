﻿using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StructLayout
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        public SolutionSettings Options { set; get; }
        private SettingsWindow Win { set; get; }

        public SettingsControl(SettingsWindow window, SolutionSettings settings)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Win = window;
            Options = settings;
            InitializeComponent();
            CreateGrid();
        }
        private void CreateGrid()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EditorUtils.EditorMode editorMode = EditorUtils.GetEditorMode();

            PropertyInfo[] properties = typeof(SolutionSettings).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                var customAttributes = (UIDescription[])property.GetCustomAttributes(typeof(UIDescription), true);
                UIDescription description = (customAttributes.Length > 0 && customAttributes[0] != null)? customAttributes[0] : null;

                if (description != null && description.EditorModeFilter != EditorUtils.EditorMode.None && description.EditorModeFilter != editorMode)
                {
                    continue;
                }

                var elementGrid = new Grid();
                elementGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(170) });
                elementGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1,GridUnitType.Star) });

                var label = new Label();
                label.Content = description == null || description.Label == null ? property.Name : description.Label;
                label.ToolTip = description == null? null : description.Tooltip;

                Grid.SetColumn(label, 0);
                elementGrid.Children.Add(label);

                if (property.PropertyType == typeof(string))
                {
                    var inputControl = new TextBox();
                    inputControl.VerticalAlignment = VerticalAlignment.Center;
                    inputControl.SetBinding(TextBox.TextProperty, new Binding("Options."+property.Name));
                    inputControl.Margin = new Thickness(5);
                    Grid.SetColumn(inputControl, 1);
                    elementGrid.Children.Add(inputControl);
                }
                else if (property.PropertyType == typeof(bool))
                {
                    var inputControl = new CheckBox();
                    inputControl.VerticalAlignment = VerticalAlignment.Center;
                    inputControl.SetBinding(CheckBox.IsCheckedProperty,new Binding("Options." + property.Name));
                    inputControl.Margin = new Thickness(5);
                    Grid.SetColumn(inputControl, 1);
                    elementGrid.Children.Add(inputControl);
                }

                optionStack.Children.Add(elementGrid);
            }
        }

        private void ApplyChanges()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var manager = SettingsManager.Instance;
            manager.Settings = Options;
            manager.Save();
        }

        public void ButtonSave_OnClick(object sender, object e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ApplyChanges();
            Win.Close();
        }

        private void ButtonDocumentation_OnClick(object sender, object e)
        {
            Documentation.OpenLink(Documentation.Link.GeneralConfiguration);
        }
    }
}

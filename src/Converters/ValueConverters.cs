using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FFmpegFrameExtractor.Models;

namespace FFmpegFrameExtractor.Converters;

/// <summary>Converts bool to Visibility (true → Visible).</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility.Visible;
}

/// <summary>Converts bool to Visibility (true → Collapsed, inverted).</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public static readonly InverseBoolToVisibilityConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility.Collapsed;
}

/// <summary>Inverts a bool value.</summary>
public class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b ? !b : value;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is bool b ? !b : value;
}

/// <summary>Converts null to Visibility.Collapsed.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Converts null to bool (not-null = true, null = false).</summary>
public class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value != null;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Converts Enum value to bool for RadioButton binding. ConverterParameter = enum member name.</summary>
public class EnumToBoolConverter : IValueConverter
{
    public static readonly EnumToBoolConverter Instance = new();
    public object Convert(object value, Type t, object param, CultureInfo c)
        => value?.ToString() == param?.ToString();
    public object ConvertBack(object value, Type t, object param, CultureInfo c)
        => value is true && param != null ? Enum.Parse(t, param.ToString()!) : System.Windows.Data.Binding.DoNothing;
}

/// <summary>Converts a file path to its file name part.</summary>
public class PathToFileNameConverter : IValueConverter
{
    public static readonly PathToFileNameConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is string s ? System.IO.Path.GetFileName(s) : value;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Converts double 0.0-100.0 to percentage string "X.X%".</summary>
public class PercentageConverter : IValueConverter
{
    public static readonly PercentageConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is double d ? $"{d:F1}%" : "0%";
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Converts ExtractionMode to its UI display label.</summary>
public class ExtractionModeDisplayConverter : IValueConverter
{
    public static readonly ExtractionModeDisplayConverter Instance = new();

    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is ExtractionMode m ? m switch
        {
            ExtractionMode.EveryNSeconds => "Every N Seconds",
            ExtractionMode.FrameRate => "Fixed FPS Rate",
            ExtractionMode.SpecificTimestamps => "Specific Timestamps",
            ExtractionMode.TimeRange => "Time Range",
            ExtractionMode.EveryNthFrame => "Every Nth Frame",
            ExtractionMode.EvenlyDistributed => "Evenly Distributed",
            ExtractionMode.SingleFrame => "Single Frame",
            ExtractionMode.SceneChange => "Scene Change Detection",
            _ => m.ToString(),
        } : value;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Converts OutputFormat to display string.</summary>
public class OutputFormatDisplayConverter : IValueConverter
{
    public static readonly OutputFormatDisplayConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is OutputFormat f ? f.ToString().ToUpper() : value;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Converts quality preset to a display label with numeric value.</summary>
public class QualityPresetDisplayConverter : IValueConverter
{
    public static readonly QualityPresetDisplayConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is QualityPreset q ? $"{q} ({(int)q})" : value;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Returns a green or red brush based on bool success value.</summary>
public class SuccessBrushConverter : IValueConverter
{
    public static readonly SuccessBrushConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36));
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Converts an empty string to Visibility.Collapsed.</summary>
public class EmptyStringToCollapsedConverter : IValueConverter
{
    public static readonly EmptyStringToCollapsedConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Converts int count > 0 to Visibility.Visible.</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public static readonly CountToVisibilityConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

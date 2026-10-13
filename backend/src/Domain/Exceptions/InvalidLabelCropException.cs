using System.Globalization;

namespace DjVisualizer.Domain.Exceptions;

public sealed class InvalidLabelCropException(double focusX, double focusY, double zoom)
    : DomainException(string.Create(
        CultureInfo.InvariantCulture,
        $"Label crop (focus {focusX}, {focusY}; zoom {zoom}) is out of range: focus must be between 0 and 1 and zoom between 1 and 4."));

using System.Collections.Generic;

namespace MauiApp1.Components.Pages;

public sealed record ProfileCriterionToggle(List<string> SelectedCodes, string Code, bool IsSelected);

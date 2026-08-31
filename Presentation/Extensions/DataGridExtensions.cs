using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NetStatAnalyzer.Presentation.Extensions
{
    public static class DataGridExtensions
    {
        public static void AutoFitColumns(this DataGrid? dataGrid, string? excludeHeader = "AÇÕES")
        {
            if (dataGrid == null || dataGrid.Columns.Count == 0) return;

            dataGrid.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var column in dataGrid.Columns)
                {
                    if (excludeHeader != null && column.Header?.ToString() == excludeHeader)
                        continue;

                    column.Width = new DataGridLength(0, DataGridLengthUnitType.Auto);
                    column.Width = DataGridLength.Auto;
                }
            }), DispatcherPriority.Loaded);
        }
    }
}

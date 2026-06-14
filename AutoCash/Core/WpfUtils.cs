using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoCash.Core
{
    public static class WpfUtils
    {
        // Утилита для снятия выделения с DataGrid при клике на пустое пространство
        public static void HandleDataGridMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dg)
            {
                HitTestResult hitTestResult = VisualTreeHelper.HitTest(dg, e.GetPosition(dg));
                if (hitTestResult != null)
                {
                    DependencyObject obj = hitTestResult.VisualHit;
                    bool clickedOnRowOrHeaderOrScroll = false;
                    while (obj != null && obj != dg)
                    {
                        if (obj is DataGridRow || obj is DataGridColumnHeader || obj is ScrollBar)
                        {
                            clickedOnRowOrHeaderOrScroll = true;
                            break;
                        }
                        obj = VisualTreeHelper.GetParent(obj);
                    }
                    
                    if (!clickedOnRowOrHeaderOrScroll)
                    {
                        dg.UnselectAll();
                    }
                }
            }
        }

        // Универсальный парсер для чисел с плавающей точкой (работает с точкой и запятой независимо от региона ОС)
        public static bool TryParseDecimal(string input, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string cleaned = input.Trim();
            
            // Сначала пробуем парсинг с текущей культурой системы
            if (decimal.TryParse(cleaned, out result)) return true;

            // Подменяем разделители, чтобы обработать случаи несовпадения локали
            if (cleaned.Contains("."))
            {
                cleaned = cleaned.Replace(".", ",");
            }
            else if (cleaned.Contains(","))
            {
                cleaned = cleaned.Replace(",", ".");
            }

            if (decimal.TryParse(cleaned, out result)) return true;

            // В качестве последней попытки пробуем инвариантную культуру
            return decimal.TryParse(input.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }
}

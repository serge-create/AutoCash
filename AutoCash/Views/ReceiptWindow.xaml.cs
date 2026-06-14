using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoCash.Core;
using AutoCash.Models;

namespace AutoCash.Views
{
    public partial class ReceiptWindow : Window
    {
        public ReceiptWindow(
            int receiptId,
            List<CartItem> items,
            decimal total,
            string paymentMethod,
            decimal amountPaid)
        {
            InitializeComponent();
            FillReceipt(receiptId, items, total, paymentMethod, amountPaid);
        }

        private void FillReceipt(
            int receiptId,
            List<CartItem> items,
            decimal total,
            string paymentMethod,
            decimal amountPaid)
        {
            var now = DateTime.Now;

            // ── Шапка ──────────────────────────────────────
            txtDate.Text       = now.ToString("dd.MM.yyyy");
            txtTime.Text       = now.ToString("HH:mm:ss");
            txtCashier.Text    = AppState.CurrentUser?.FullName ?? "—";
            txtShift.Text      = AppState.CurrentShift?.ShiftID.ToString() ?? "—";
            txtReceiptNum.Text = receiptId.ToString("D6");

            // ── Тип оплаты ─────────────────────────────────
            bool isCash = paymentMethod == "Наличные";
            lblPayMethod.Text = isCash ? "НАЛИЧНЫЕ:" : "КАРТА:";
            txtPaid.Text      = $"{(isCash ? amountPaid : total):F2} руб.";

            decimal change   = isCash ? (amountPaid - total) : 0m;
            txtChange.Text   = $"{change:F2} руб.";
            rowChange.Visibility = isCash ? Visibility.Visible : Visibility.Collapsed;

            txtTotal.Text = $"{total:F2} руб.";

            // ── Итог скидки (показываем только если хоть одна позиция со скидкой) ──
            decimal origTotal    = items.Sum(i => i.Price * i.Quantity);
            decimal totalSaved   = origTotal - total;
            if (totalSaved > 0m)
            {
                rowOrigTotal.Visibility    = Visibility.Visible;
                rowTotalDiscount.Visibility = Visibility.Visible;
                txtOrigTotal.Text          = $"{origTotal:F2} руб.";
                txtTotalDiscount.Text      = $"-{totalSaved:F2} руб.";
            }

            // ── ФН реквизиты (заглушка) ────────────────────
            txtFD.Text = $"ФД: {receiptId:D6}";
            txtFP.Text = $"ФП: {Math.Abs(now.GetHashCode() ^ receiptId):D10}";

            // ── Товары ─────────────────────────────────────
            spItems.Children.Clear();
            int lineNum = 1;
            foreach (var item in items)
            {
                // — Строка 1: Номер + Название товара
                var nameBlock = MakeText($"{lineNum}. {item.ProductName}", "#111111", 11, wrap: true, bold: true);
                nameBlock.Margin = new Thickness(0, 5, 0, 1);
                spItems.Children.Add(nameBlock);

                // — Строка 2: Кол × ОригЦена  →  ИтогСумма (5 колонок)
                var row = new Grid { Margin = new Thickness(0, 0, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) }); // кол
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); // ×
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) }); // цена
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) }); // сумма

                SetCell(row, "",                           0, "#888888");
                SetCell(row, $"{item.Quantity:G}",         1, "#333333", TextAlignment.Right);
                SetCell(row, "×",                          2, "#888888", TextAlignment.Center);
                SetCell(row, $"{item.Price:F2}",           3, "#333333", TextAlignment.Right);
                SetCell(row, $"{item.Total:F2}",           4, "#111111", TextAlignment.Right, bold: true);
                spItems.Children.Add(row);

                // — Строка 3 (если есть скидка): "-Скидка X% → -сумма_скидки"
                if (item.Discount > 0m)
                {
                    decimal savedPerUnit = item.Price - item.DiscountedPrice;
                    decimal savedTotal   = savedPerUnit * item.Quantity;

                    var discRow = new Grid { Margin = new Thickness(0, 0, 0, 0) };
                    discRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    discRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

                    SetCell(discRow, $"  СКИДКА {item.Discount:0.##}%", 0, "#B05000", TextAlignment.Left);
                    SetCell(discRow, $"-{savedTotal:F2}",                1, "#B05000", TextAlignment.Right);
                    spItems.Children.Add(discRow);
                }

                // — Строка 4: Налоговая ставка
                string taxLabel = string.IsNullOrEmpty(item.VatRateName)
                    ? "НДС не указан"
                    : $"{item.VatRateName} ({item.VatRateValue:0.##}%)";

                var taxRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                taxRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                taxRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

                // НДС изнутри для этой строки
                decimal itemVat = item.VatRateValue > 0
                    ? Math.Round(item.Total * item.VatRateValue / (100m + item.VatRateValue), 2)
                    : 0m;
                string vatStr = item.VatRateValue > 0 ? $"НДС: {itemVat:F2}" : "НДС: —";

                SetCell(taxRow, $"  {taxLabel}", 0, "#777777", TextAlignment.Left);
                SetCell(taxRow, vatStr,           1, "#777777", TextAlignment.Right);
                spItems.Children.Add(taxRow);

                lineNum++;
            }

            // ── Налоги по ставкам из БД ────────────────────
            FillTaxSection(items);
        }

        /// <summary>
        /// Группирует позиции чека по налоговой ставке и отображает
        /// оборот + сумму налога для каждой ставки из БД.
        /// </summary>
        private void FillTaxSection(List<CartItem> items)
        {
            spTaxLines.Children.Clear();

            // Группируем по (RateID, Name, RateValue)
            var groups = items
                .GroupBy(i => new { i.VatRateName, i.VatRateValue })
                .OrderBy(g => g.Key.VatRateValue)
                .ToList();

            decimal totalVat    = 0m;
            decimal totalNoVat  = 0m;

            foreach (var g in groups)
            {
                decimal turnover = g.Sum(i => i.Total);
                decimal rate     = g.Key.VatRateValue;
                decimal vatAmt;

                if (rate == 0m)
                {
                    // Без НДС
                    vatAmt = 0m;
                }
                else
                {
                    // НДС считается «изнутри»: НДС = оборот × rate / (100 + rate)
                    vatAmt = Math.Round(turnover * rate / (100m + rate), 2);
                }

                decimal baseAmt = turnover - vatAmt;
                totalVat   += vatAmt;
                totalNoVat += baseAmt;

                string rateName = string.IsNullOrEmpty(g.Key.VatRateName)
                    ? $"НДС {rate:0}%"
                    : g.Key.VatRateName;

                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

                SetCell(row, rateName,              0, "#333333", TextAlignment.Left);
                SetCell(row, $"{turnover:F2}",      1, "#333333", TextAlignment.Right);
                SetCell(row, vatAmt == 0m ? "—" : $"{vatAmt:F2}", 2, "#333333", TextAlignment.Right);

                spTaxLines.Children.Add(row);
            }

            // Итоговая строка НДС
            if (groups.Count > 0)
            {
                var sep = MakeText(".........................................", "#BBBBBB", 10);
                sep.TextAlignment = TextAlignment.Center;
                sep.Margin = new Thickness(0, 2, 0, 2);
                spTaxLines.Children.Add(sep);

                var totRow = new Grid();
                totRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                totRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                totRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

                SetCell(totRow, "ВКЛ. НДС ИТОГО:", 0, "#111111", TextAlignment.Left,  bold: true);
                SetCell(totRow, "",                  1, "#111111", TextAlignment.Right);
                SetCell(totRow, $"{totalVat:F2}",    2, "#111111", TextAlignment.Right, bold: true);
                spTaxLines.Children.Add(totRow);
            }
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private TextBlock MakeText(string text, string color, double size,
            bool wrap = false, bool bold = false)
        {
            return new TextBlock
            {
                Text        = text,
                FontFamily  = new FontFamily("Courier New"),
                FontSize    = size,
                Foreground  = (Brush)new BrushConverter().ConvertFrom(color),
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                FontWeight  = bold ? FontWeights.Bold : FontWeights.Normal
            };
        }

        private void SetCell(Grid grid, string text, int col,
            string color, TextAlignment align = TextAlignment.Left, bool bold = false)
        {
            var tb = MakeText(text, color, 11, bold: bold);
            tb.TextAlignment = align;
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}

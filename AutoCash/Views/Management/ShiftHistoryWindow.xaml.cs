using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using AutoCash.Core;
using AutoCash.Models;

namespace AutoCash.Views.Management
{
        
    public partial class ShiftHistoryWindow : Window
    {
        
        public ShiftHistoryWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Безопасность: только Администратор может просматривать денежную аналитику
            if (AppState.CurrentUser?.Roles?.RoleName != "Администратор")
            {
                
                MessageBox.Show("Доступ к финансовой аналитике разрешен только Администратору системы.",
                                "Доступ ограничен", MessageBoxButton.OK, MessageBoxImage.Stop);
                this.Close();
                return;
            }

            // Устанавливаем дефолтный диапазон: с начала текущего месяца по сегодняшний день
            dpStart.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            dpEnd.SelectedDate = DateTime.Now;

            LoadShiftAnalytics();
        }

        // Центральный метод загрузки и расчета данных
        private void LoadShiftAnalytics()
        {
            DateTime? startDate = dpStart.SelectedDate;
            // Конец дня выбранной даты, чтобы захватить вечерние смены
            DateTime? endDate = dpEnd.SelectedDate?.AddDays(1).AddTicks(-1);

            try
            {
                using (var db = new Models.AutoCashierDbEntities1())
                {
                    // Базовый LINQ-запрос с жадной загрузкой сотрудников
                    var shiftsQuery = db.Shifts.Include(s => s.Employees).AsQueryable();

                    // Применяем фильтрацию по датам, если они выбраны
                    if (startDate.HasValue)
                    {
                        shiftsQuery = shiftsQuery.Where(s => s.OpenedAt >= startDate.Value);
                    }
                    if (endDate.HasValue)
                    {
                        shiftsQuery = shiftsQuery.Where(s => s.OpenedAt <= endDate.Value);
                    }

                    // Выгружаем смены и на лету вычисляем аналитические показатели
                    var rawShifts = shiftsQuery.OrderByDescending(s => s.OpenedAt).ToList();

                    var displayList = rawShifts.Select(s => {
                        // Рассчитываем сумму чеков для данной смены
                        // Суммируем поле TotalAmount (или то, которое обновили в Receipts)
                        decimal shiftRevenue = db.Receipts
                                                 .Where(r => r.ShiftID == s.ShiftID)
                                                 .Sum(r => (decimal?)r.TotalAmount) ?? 0m;

                        return new ShiftAnalyticsViewModel
                        {
                            ShiftID = s.ShiftID,
                            CashierName = s.Employees?.FullName ?? "Системная запись",
                            OpenedAt = s.OpenedAt,
                            ClosedAt = s.ClosedAt,
                            Status = s.ClosedAt.HasValue ? "Закрыта" : "В работе",
                            TotalRevenue = shiftRevenue
                        };
                    }).ToList();

                    // Выводим результат в таблицу
                    dgShifts.ItemsSource = displayList;

                    // Считаем общие итоги
                    txtTotalShifts.Text = displayList.Count.ToString();
                    decimal grandTotal = displayList.Sum(item => item.TotalRevenue);
                    txtGrandTotalRevenue.Text = $"{grandTotal:F2} ₽";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка расчета аналитических данных:\n{ex.Message}",
                                "Ошибка СУБД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            LoadShiftAnalytics();
        }

        private void btnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            dpStart.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            dpEnd.SelectedDate = DateTime.Now;
            LoadShiftAnalytics();
        }
    }

    // Специализированная DTO-модель для красивого отображения данных в DataGrid
    public class ShiftAnalyticsViewModel
    {
        public int ShiftID { get; set; }
        public string CashierName { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string Status { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
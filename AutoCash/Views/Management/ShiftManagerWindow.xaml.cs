using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AutoCash.Core;
using AutoCash.Models; // Твои классы Entity Framework

namespace AutoCash.Views.Management
{
    public partial class ShiftManagerWindow : Window
    {
        public ShiftManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUI();
        }

        // Метод обновления интерфейса в зависимости от состояния смены
        private void UpdateUI()
        {
            txtCashierName.Text = AppState.CurrentUser?.FullName ?? "Неизвестно";

            if (AppState.CurrentShift == null)
            {
                // Смена закрыта
                borderStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")); // Серый
                txtStatus.Text = "СМЕНА ЗАКРЫТА";

                txtOpenTime.Text = "-";
                txtRevenue.Text = "0.00 ₽";

                btnOpenShift.IsEnabled = true;
                btnOpenShift.Opacity = 1;

                btnCloseShift.IsEnabled = false;
                btnCloseShift.Opacity = 0.5;
            }
            else
            {
                // Смена открыта
                borderStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")); // Синий
                txtStatus.Text = "СМЕНА ОТКРЫТА";

                txtOpenTime.Text = AppState.CurrentShift.OpenedAt.ToString("dd.MM.yyyy HH:mm:ss");

                // Здесь в идеале мы должны запросить сумму из БД по пробитым чекам за эту смену.
                // Для базовой версии выведем просто заглушку или расчетное поле из смены.
                txtRevenue.Text = "Расчет...";

                btnOpenShift.IsEnabled = false;
                btnOpenShift.Opacity = 0.5;

                btnCloseShift.IsEnabled = true;
                btnCloseShift.Opacity = 1;

                CalculateRevenue();
            }
        }

        // Подсчет выручки за текущую открытую смену (поиск чеков)
        private void CalculateRevenue()
        {
            if (AppState.CurrentShift == null) return;

            try
            {
                using (var db = new Models.AutoCashierDbEntities1()) // Замени на свое имя Context
                {
                    // Ищем все чеки, привязанные к текущей смене
                    var totalRev = db.Receipts
                                     .Where(r => r.ShiftID == AppState.CurrentShift.ShiftID)
                                     .Sum(r => (decimal?)r.TotalAmount) ?? 0m;

                    txtRevenue.Text = $"{totalRev:F2} ₽";
                }
            }
            catch
            {
                txtRevenue.Text = "Ошибка расчета";
            }
        }

        // Обработчик: Открыть смену
        private void btnOpenShift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var db = new Models.AutoCashierDbEntities1())
                {
                    // Создаем новую запись о смене в БД
                    var newShift = new Shifts
                    {
                        EmployeeID = AppState.CurrentUser.EmployeeID,
                        OpenedAt = DateTime.Now
                        // Если в таблице есть другие обязательные поля (например CashRegisterID), заполни их здесь
                    };

                    db.Shifts.Add(newShift);
                    db.SaveChanges(); // Сохраняем, чтобы получить сгенерированный ID

                    // Записываем открытую смену в глобальную память
                    AppState.CurrentShift = newShift;

                    MessageBox.Show("Кассовая смена успешно открыта! Можно начинать продажи.", "Открытие смены", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateUI();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии смены: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработчик: Закрыть смену (Z-Отчет)
        private void btnCloseShift_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите закрыть смену и снять Z-отчет?\nПосле этого пробивать чеки будет невозможно.",
                                         "Закрытие смены", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var db = new Models.AutoCashierDbEntities1())
                    {
                        // Находим текущую смену в базе
                        var shiftToClose = db.Shifts.Find(AppState.CurrentShift.ShiftID);
                        if (shiftToClose != null)
                        {
                            shiftToClose.ClosedAt = DateTime.Now;
                            

                            // Симуляция связи с онлайн-кассой: печать фискального Z-отчета
                            PrintZReport();

                            db.SaveChanges();
                        }

                        // Очищаем текущую смену в глобальной памяти
                        AppState.CurrentShift = null;

                        MessageBox.Show("Смена закрыта. Z-отчет распечатан.", "Закрытие смены", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateUI();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при закрытии смены: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PrintZReport()
        {
            // Здесь в реальной жизни был бы код отправки команды на драйвер АТОЛ или Штрих-М.
            // Для диплома оставляем как логическую заглушку.
        }
    }
}
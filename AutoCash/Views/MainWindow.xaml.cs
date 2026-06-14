using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AutoCash.Core;
using AutoCash.Models;

namespace AutoCash.Views
{
    public partial class MainWindow : Window
    {
        // Коллекция корзины покупок (автоматически обновляет UI интерфейс)
        private ObservableCollection<CartItem> _cartItems = new ObservableCollection<CartItem>();

        // Строковый буфер для сбора цифр со сканера штрихкодов
        private string _barcodeBuffer = "";

        // Текущая сумма чека
        private decimal _currentTotal = 0;

        // Сумма, внесённая покупателем (для сдачи)
        private decimal _amountPaid = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppState.CurrentUser == null)
            {
                MessageBox.Show("Ошибка авторизации. Выполните вход.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Logout();
                return;
            }

            txtCurrentUser.Text = $"Кассир: {AppState.CurrentUser.FullName} | Роль: {AppState.CurrentUser.Roles?.RoleName ?? "Неизвестно"}";

            // ПРИВЯЗКА КОРЗИНЫ К ТАБЛИЦЕ
            dgReceipt.ItemsSource = _cartItems;

            ApplyPermissions();
        }

        private void ApplyPermissions()
        {
            string roleName = AppState.CurrentUser.Roles?.RoleName;
            if (roleName == "Кассир")
            {
                btnProducts.Visibility = Visibility.Collapsed;
                btnEmployees.Visibility = Visibility.Collapsed;
                btnShifts.Visibility = Visibility.Collapsed;
                btnAnalytics.Visibility = Visibility.Collapsed;
            }
            else if (roleName == "Старший кассир")
            {
                btnEmployees.Visibility = Visibility.Collapsed;
            }
        }

        private void dgReceipt_MouseDown(object sender, MouseButtonEventArgs e)
        {
            AutoCash.Core.WpfUtils.HandleDataGridMouseDown(sender, e);
        }

        // ==========================================
        // ЛОГИКА СКАНИРОВАНИЯ И ДОБАВЛЕНИЯ В ЧЕК
        // ==========================================

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Обработка горячих клавиш оплаты (F9 / F10)
            if (e.Key == Key.F3) { btnSearchProduct_Click(null, null); return; }
            if (e.Key == Key.F9) { btnPayCash_Click(null, null); return; }
            if (e.Key == Key.F8) { btnPayCard_Click(null, null); return; }
            {
                
            }

            // Логика сканера штрихкодов (Считывает Enter в конце)
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (!string.IsNullOrEmpty(_barcodeBuffer))
                {
                    ProcessBarcode(_barcodeBuffer);
                    _barcodeBuffer = "";
                }
            }
            else
            {
                // Собираем цифры, если нажата клавиша с цифрой (основная или Numpad)
                if (e.Key >= Key.D0 && e.Key <= Key.D9)
                    _barcodeBuffer += (e.Key - Key.D0).ToString();
                else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                    _barcodeBuffer += (e.Key - Key.NumPad0).ToString();
            }
        }

        private void ProcessBarcode(string barcode)
        {
            // Защита: нельзя пробивать чек, если смена не открыта
            if (AppState.CurrentShift == null)
            {
                MessageBox.Show("Смена не открыта! Перейдите в 'Кассовые смены' и откройте смену.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new AutoCashierDbEntities1())
                {
                    // Загружаем товар вместе с налоговой ставкой
                    var product = db.Products
                        .Include(p => p.Tax_Rates)
                        .FirstOrDefault(p => p.Barcode == barcode);

                    if (product != null)
                        AddToCart(product);
                    else
                        MessageBox.Show($"Товар со штрихкодом {barcode} не найден в базе!", "Не найдено", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка БД: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddToCart(Products product)
        {
            var existingItem = _cartItems.FirstOrDefault(c => c.ProductID == product.ProductID);

            if (existingItem != null)
            {
                existingItem.Quantity++;
                // Используем цену с учётом скидки (если есть)
                existingItem.Total = existingItem.Quantity * existingItem.DiscountedPrice;
                dgReceipt.Items.Refresh();
            }
            else
            {
                decimal discountPct     = product.DiscountPercent ?? 0m;
                decimal discountedPrice = product.Price * (1m - discountPct / 100m);

                _cartItems.Add(new CartItem
                {
                    ProductID      = product.ProductID,
                    ProductName    = product.Name,
                    Quantity       = 1,
                    Price          = product.Price,          // Оригинальная цена (до скидки)
                    Discount       = discountPct,            // Скидка в %
                    DiscountedPrice = discountedPrice,       // Цена после скидки
                    Total          = discountedPrice,        // 1 шт × цена со скидкой
                    VatRateName    = product.Tax_Rates?.Name ?? "",
                    VatRateValue   = product.Tax_Rates?.RateValue ?? 0m
                });
            }

            UpdateTotal();
        }

        // Обновление итоговой суммы
        private void UpdateTotal()
        {
            _currentTotal = _cartItems.Sum(c => c.Total);
            txtTotalAmount.Text = $"{_currentTotal:F2} ₽";
        }

        // ==========================================
        // БЫСТРЫЕ ДЕЙСТВИЯ (ПРАВАЯ ПАНЕЛЬ)
        // ==========================================

        private void btnStorno_Click(object sender, RoutedEventArgs e)
        {
            if (dgReceipt.SelectedItem is CartItem selectedItem)
            {
                _cartItems.Remove(selectedItem);
                UpdateTotal();
            }
            else
            {
                MessageBox.Show("Выберите строку в чеке для сторнирования (удаления).", "Подсказка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnClearReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count > 0)
            {
                var res = MessageBox.Show("Вы уверены, что хотите очистить весь чек?", "Очистка", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _cartItems.Clear();
                    UpdateTotal();
                }
            }
        }

        private void btnSearchProduct_Click(object sender, RoutedEventArgs e)
        {
            // Защита: нельзя искать и пробивать, если смена закрыта
            if (AppState.CurrentShift == null)
            {
                MessageBox.Show("Смена не открыта! Перейдите в 'Кассовые смены' и откройте смену.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProductSearchWindow searchWindow = new ProductSearchWindow();
            searchWindow.Owner = this;

            // Открываем окно. Если кассир выбрал товар, DialogResult будет true
            if (searchWindow.ShowDialog() == true)
            {
                if (searchWindow.SelectedProduct != null)
                {
                    // Используем уже готовый метод добавления в корзину!
                    AddToCart(searchWindow.SelectedProduct);
                }
            }
        }

        // ==========================================
        // НАВИГАЦИЯ И ВЫХОД
        // ==========================================
        private void btnLogout_Click(object sender, RoutedEventArgs e) => Logout();

        private void Logout()
        {
            AppState.Logout();
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
        // ==========================================
        // ЛОГИКА ОПЛАТЫ И СОХРАНЕНИЯ ЧЕКА
        // ==========================================

        private void btnPayCash_Click(object sender, RoutedEventArgs e) => ProcessPayment("Наличные");
        private void btnPayCard_Click(object sender, RoutedEventArgs e) => ProcessPayment("Карта");

        private void ProcessPayment(string paymentMethod)
        {
            // 1. Проверяем, открыта ли смена
            if (AppState.CurrentShift == null)
            {
                MessageBox.Show("Смена не открыта! Перейдите в 'Кассовые смены' и откройте смену.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Проверяем, есть ли товары в чеке
            if (_cartItems.Count == 0)
            {
                MessageBox.Show("Чек пуст. Добавьте товары для оплаты.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Открываем наше окно оплаты
            PaymentWindow paymentWin = new PaymentWindow(_currentTotal, paymentMethod);
            paymentWin.Owner = this;

            // 4. Если кассир успешно пробил чек (нажал Enter в PaymentWindow)
            if (paymentWin.ShowDialog() == true)
            {
                // Запоминаем сумму оплаты (для наличных — введённую сумму, для карты — точную)
                _amountPaid = paymentMethod == "Наличные" ? paymentWin.AmountReceived : _currentTotal;
                SaveReceiptToDatabase(paymentMethod);
            }
        }

        private void SaveReceiptToDatabase(string paymentMethod)
        {
            MessageBox.Show(paymentMethod);
            try
            {
                using (var db = new AutoCashierDbEntities1()) // Убедись, что имя контекста твоё
                {
                    // Получаем ID типа оплаты из БД (например: 1 - Наличные, 2 - Карта)
                    var paymentType = db.Payment_Types.Where(x => x.Name == paymentMethod).FirstOrDefault();
                    if (paymentType == null)
                    {
                        MessageBox.Show($"Ошибка: Тип оплаты '{paymentMethod}' не найден в базе данных!", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    int paymentTypeId = paymentType.PaymentTypeID;
                    // СОЗДАЕМ ЗАГОЛОВОК ЧЕКА
                    var newReceipt = new Receipts
                    {
                        ShiftID = AppState.CurrentShift.ShiftID, // ID текущей смены
                        EmployeeID = AppState.CurrentUser.EmployeeID,    // Кто пробил
                        IsReturn = false,
                        CustomerID = null, // Дополнительный функционал для коммерческой версии
                        PaymentTypeID = paymentTypeId,
                        ReceiptDiscount = 0, // Дополнительный функционал для коммерческой версии
                        CreatedAt = DateTime.Now,
                        StatusID = 1, // 1 - Приход (продажа) по 54-ФЗ
                        TotalAmount = _cartItems.Sum(c => c.Total)
                    };

                    db.Receipts.Add(newReceipt);
                    db.SaveChanges(); // Сохраняем, чтобы БД сгенерировала ReceiptID для позиций чека

                    // СОЗДАЕМ СТРОКИ ЧЕКА И СПИСЫВАЕМ ОСТАТКИ
                    foreach (var item in _cartItems)
                    {
                        // Записываем товар в чек
                        var receiptItem = new Receipt_Items
                        {
                            ReceiptID = newReceipt.ReceiptID,
                            ProductID = item.ProductID,
                            Quantity = item.Quantity,
                            Price = item.Price,
                            SubTotal = item.Total
                        };
                        db.Receipt_Items.Add(receiptItem);

                        // Списываем остаток товара со склада
                        var productInDb = db.Products.Find(item.ProductID);
                        if (productInDb != null)
                        {
                            productInDb.StockQuantity -= (int)item.Quantity;
                        }
                    }

                    db.SaveChanges(); // Сохраняем все позиции и обновленные остатки (Транзакция)

                    // --- ПОКАЗЫВАЕМ ЧЕК ---
                    var itemsSnapshot = new System.Collections.Generic.List<CartItem>(_cartItems);
                    decimal paidAmount = _amountPaid;
                    int newReceiptId = newReceipt.ReceiptID;
                    string pMethod = paymentMethod;
                    decimal totalSnap = _cartItems.Sum(c => c.Total);

                    // Очищаем до открытия окна, чтобы касса была готова
                    _cartItems.Clear();
                    UpdateTotal();

                    var receiptWin = new ReceiptWindow(newReceiptId, itemsSnapshot, totalSnap, pMethod, paidAmount);
                    receiptWin.Owner = this;
                    receiptWin.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        // Заглушки других окон (админка)
        private void btnProducts_Click(object sender, RoutedEventArgs e) { new Management.ProductManager().ShowDialog(); }
        private void btnShifts_Click(object sender, RoutedEventArgs e) { new Management.ShiftManagerWindow().ShowDialog(); }
        private void btnEmployees_Click(object sender, RoutedEventArgs e) { new Management.EmployeeManagerWindow().ShowDialog(); }

        private void btnAnalytics_Click(object sender, RoutedEventArgs e)
        {
            Management.ShiftHistoryWindow historyWindow = new Management.ShiftHistoryWindow();
            historyWindow.Owner = this;
            historyWindow.ShowDialog();
        }
    }

    // Вспомогательный класс-модель (ViewModel) для отображения строк в DataGrid
    public class CartItem
    {
        public int     ProductID      { get; set; }
        public string  ProductName    { get; set; }
        public decimal Quantity       { get; set; }
        public decimal Price          { get; set; }   // цена до скидки
        public decimal Discount       { get; set; }   // скидка, %
        public decimal DiscountedPrice { get; set; }  // цена со скидкой
        public decimal Total          { get; set; }   // итог = Quantity × DiscountedPrice
        // Налоговая информация (заполняется при добавлении из БД)
        public string  VatRateName    { get; set; }
        public decimal VatRateValue   { get; set; }
    }
}
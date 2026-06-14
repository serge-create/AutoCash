using System;
using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoCash.Views
{
    public partial class PaymentWindow : Window
    {
        private decimal _totalAmount;
        private string _paymentMethod; // "Наличные" или "Карта"

        // Сумма, которую внёс покупатель (читает MainWindow)
        public decimal AmountReceived { get; private set; }

        // Конструктор принимает итоговую сумму и выбранный метод оплаты
        public PaymentWindow(decimal totalAmount, string paymentMethod)
        {
            InitializeComponent();
            _totalAmount = totalAmount;
            _paymentMethod = paymentMethod;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Настраиваем интерфейс под сумму и тип оплаты
            txtTotalSum.Text = $"{_totalAmount:F2} ₽";
            lblPaymentType.Text = $"ОПЛАТА: {_paymentMethod.ToUpper()}";

            if (_paymentMethod == "Карта")
            {
                // При безналичном расчете сдачу и поле ввода скрываем/блокируем
                spAmountReceived.Visibility = Visibility.Collapsed;
                spChange.Visibility = Visibility.Collapsed;
                this.Height = 300; // Уменьшаем высоту окна, так как поля скрыты
                btnConfirm.Focus();
            }
            else
            {
                // При наличных — сразу фокусируемся на поле ввода денег
                txtReceived.Focus();
                txtReceived.Text = _totalAmount.ToString("F0"); // Подставляем сумму без копеек как подсказку
                txtReceived.SelectAll();
            }
        }

        // Динамический расчет сдачи при изменении текста
        private void txtReceived_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_paymentMethod == "Карта") return;

            if (AutoCash.Core.WpfUtils.TryParseDecimal(txtReceived.Text, out decimal received))
            {
                decimal change = received - _totalAmount;
                if (change >= 0)
                {
                    txtChange.Text = $"{change:F2} ₽";
                    btnConfirm.IsEnabled = true;
                }
                else
                {
                    txtChange.Text = "Недостаточно средств";
                    btnConfirm.IsEnabled = false; // Блокируем закрытие чека, пока не внесена вся сумма
                }
            }
            else
            {
                txtChange.Text = "0.00 ₽";
                btnConfirm.IsEnabled = false;
            }
        }

        // Валидация ввода: разрешаем только цифры и один разделитель (запятую/точку)
        private void txtReceived_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9,.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // Кнопка подтверждения (Пробитие чека)
        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            FinalizeReceipt();
        }

        // Кнопка отмены
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Обработка горячих клавиш (Enter и Esc)
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnConfirm.IsEnabled)
            {
                FinalizeReceipt();
            }
            else if (e.Key == Key.Escape)
            {
                btnCancel_Click(this, null);
            }
        }

        // Метод успешного завершения транзакции
        private void FinalizeReceipt()
        {
            // Сохраняем внесённую сумму
            if (_paymentMethod == "Наличные" &&
                AutoCash.Core.WpfUtils.TryParseDecimal(txtReceived.Text, out decimal received))
            {
                AmountReceived = received;
            }
            else
            {
                AmountReceived = _totalAmount;
            }

            this.DialogResult = true; // Сигнализируем MainWindow, что продажа закрыта
            this.Close();
        }
    }
}
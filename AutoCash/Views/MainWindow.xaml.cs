using System;
using System.Windows;
using System.Windows.Input;
using AutoCash.Core;

namespace AutoCash.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Событие срабатывает при загрузке окна
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Проверка: если кто-то запустил окно в обход авторизации
            if (AppState.CurrentUser == null)
            {
                MessageBox.Show("Ошибка авторизации. Выполните вход.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Logout();
                return;
            }

            // Выводим имя сотрудника и его роль на экран
            txtCurrentUser.Text = $"Кассир: {AppState.CurrentUser.FullName} | Роль: {AppState.CurrentUser.Roles?.RoleName ?? "Неизвестно"}";

            // Применяем ролевую модель (RBAC)
            ApplyPermissions();
        }

        // Разграничение прав доступа
        private void ApplyPermissions()
        {
            string roleName = AppState.CurrentUser.Roles?.RoleName;

            // Если это обычный кассир - скрываем все кнопки администрирования
            if (roleName == "Кассир")
            {
                btnProducts.Visibility = Visibility.Collapsed;
                btnEmployees.Visibility = Visibility.Collapsed;
                btnShifts.Visibility = Visibility.Collapsed;
            }
            // Если это старший кассир - скрываем только персонал
            else if (roleName == "Старший кассир")
            {
                btnEmployees.Visibility = Visibility.Collapsed;
                // Управление товарами остается, но внутри самого ProductManagerWindow мы заблокируем добавление
            }
            // Если Администратор - всё остается видимым по умолчанию
        }

        // Обработка кнопки "Сменить пользователя"
        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            Logout();
        }

        private void Logout()
        {
            AppState.Logout(); // Очищаем сессию
            LoginWindow loginWindow = new LoginWindow();
            // Так как LoginWindow у нас Page, для него потребуется контейнер, либо можно сделать LoginWindow как Window.
            // Если LoginWindow был создан как Window (исходя из логики десктопного приложения), вызываем:
            // loginWindow.Show(); 
            this.Close();
        }

        // Заглушки для переходов (создадим эти окна позже)
        private void btnProducts_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Модуль в разработке"); }
        private void btnShifts_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Модуль в разработке"); }
        private void btnEmployees_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Модуль в разработке"); }

        // Здесь будем перехватывать штрихкоды со сканера
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Место для Этапа 4: сканирование товара
        }
    }
}
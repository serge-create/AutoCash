using System;
using AutoCash.Models; // Подключаем твои классы из базы данных

namespace AutoCash.Core
{
    // Статический класс для хранения глобального состояния приложения (сессии)
    public static class AppState
    {
        // Текущий авторизованный сотрудник
        public static Employees CurrentUser { get; set; }

        // Текущая открытая кассовая смена (то, чего нам не хватало!)
        public static Shifts CurrentShift { get; set; }

        // Метод для очистки сессии при смене пользователя
        public static void Logout()
        {
            CurrentUser = null;
            CurrentShift = null;
        }
    }
}
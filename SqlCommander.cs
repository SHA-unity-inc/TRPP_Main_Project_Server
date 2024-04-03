using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Numerics;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Npgsql;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace shooter_server
{
    public class SqlCommander
    {
        private string host;
        private string user;
        private string password;
        private string database;
        private int port;

        public SqlCommander(string host, string user, string password, string database, int port)
        {
            this.host = host;
            this.user = user;
            this.password = password;
            this.database = database;
            this.port = port;
        }

        public async Task ExecuteSqlCommand(Lobby lobby, WebSocket webSocket, string sqlCommand, Player player)
        {
            Console.WriteLine(sqlCommand);
            // Создание соединения с базой данных
            using (var dbConnection = new NpgsqlConnection($"Host={host};Username={user};Password={password};Database={database};Port={port}"))
            {
                await dbConnection.OpenAsync();
                Console.WriteLine(dbConnection.ConnectionString);

                // Создание курсора
                using (var cursor = dbConnection.CreateCommand())
                {
                    int senderId = player.Id;

                    try
                    {
                        // Определение типа SQL-команды
                        switch (sqlCommand)
                        {
                            case string s when s.StartsWith("Login"):
                                ExecuteLogin(sqlCommand, cursor, senderId, dbConnection, lobby, webSocket);
                                break;
                            case string s when s.StartsWith("Registration"):
                                ExecuteRegistration(sqlCommand, cursor, senderId, dbConnection, player);
                                break;
                            case string s when s.StartsWith("GetID"):
                                lobby.SendMessagePlayer($"/cmdGetID {senderId}", webSocket);
                                break;
                            default:
                                Console.WriteLine("Command not found");
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error executing SQL command: {e}");
                    }
                }
            }
        }

        private void ExecuteLogin(string sqlCommand, NpgsqlCommand cursor, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            try
            {
                // Убираем "Login" из начала SQL-команды
                sqlCommand = sqlCommand.Substring(6);
                // Парсим JSON и извлекаем данные
                string[] credentials = sqlCommand.Split(" ");
                string username = credentials[0], password = credentials[1];

                // Проверка, что пользователь с таким именем существует
                cursor.CommandText = $"SELECT * FROM users WHERE name='{username}'";
                using (NpgsqlDataReader reader = cursor.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string storedPassword = reader.GetString(2);

                        if (password == storedPassword)
                        {
                            int userId = reader.GetInt32(0);
                            // Сохраняем id в экземпляре SqlCommander
                            lobby.Players[ws].Id = userId;
                            // Вызываем add_player и передаем id
                            lobby.SendMessageExcept($"Welcome, Player {lobby.Players[ws].Id}", ws);
                            lobby.SendMessagePlayer($"/ans true", ws);
                            SendLoginResponse(senderId, userId, "success");
                        }
                        else
                        {
                            SendLoginResponse(senderId, -1, "error", "Invalid password");
                            lobby.SendMessagePlayer($"/ans false", ws);
                        }
                    }
                    else
                    {
                        reader.Close();
                        // Пользователь с таким именем не существует
                        SendLoginResponse(senderId, -1, "error", "User not found");
                    }
                }
            }
            catch (Exception e)
            {
                SendLoginResponse(senderId, -1, "error", "User not found");
                Console.WriteLine($"Error executing Login command: {e}");
            }
        }

        private void ExecuteRegistration(string sqlCommand, NpgsqlCommand cursor, int senderId, NpgsqlConnection dbConnection, Player player)
        {
            try
            {
                // Убираем "Registration" из начала SQL-команды
                sqlCommand = sqlCommand.Substring(13);
                // Парсим JSON и извлекаем данные
                string[] credentials = sqlCommand.Split(" ");
                string username = credentials[0], password = credentials[1];

                // Начало транзакции
                using (var transaction = dbConnection.BeginTransaction())
                {
                    try
                    {
                        // Проверка, что пользователь с таким именем не существует
                        cursor.CommandText = $"SELECT * FROM users WHERE username='{username}'";
                        using (NpgsqlDataReader reader = cursor.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                reader.Close();
                                // Генерируем случайную соль
                                string salt = Guid.NewGuid().ToString("N").Substring(0, 16);

                                // Добавляем соль к паролю
                                string saltedPassword = password + salt;

                                // Создаем объект хэша с использованием алгоритма SHA-256
                                using (SHA256 sha256 = SHA256.Create())
                                {
                                    // Кодируем соленый пароль в байтовую строку перед передачей его объекту хэша
                                    byte[] saltedPasswordBytes = Encoding.UTF8.GetBytes(saltedPassword);

                                    // Обновляем объект хэша с байтами соленого пароля
                                    byte[] hashedPasswordBytes = sha256.ComputeHash(saltedPasswordBytes);

                                    // Получаем шестнадцатеричное представление хэша
                                    string hashedPassword = BitConverter.ToString(hashedPasswordBytes).Replace("-", "");

                                    // Регистрация пользователя
                                    Console.WriteLine($"('{username}', '{hashedPassword}', '{salt}')");
                                    cursor.CommandText = $"INSERT INTO users (username, password, salt) VALUES ('{username}', '{hashedPassword}', '{salt}')";
                                    cursor.ExecuteNonQuery();

                                    // Подтверждение изменений
                                    transaction.Commit();

                                    SendRegistrationResponse(senderId, "success");
                                }
                            }
                            else
                            {
                                // Пользователь с таким именем уже существует
                                SendRegistrationResponse(senderId, "error", "Username already exists");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // В случае ошибки откатываем транзакцию
                        transaction.Rollback();
                        Console.WriteLine($"Error executing Registration command: {e}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error executing Registration command: {e}");
            }
        }


        private void SendLoginResponse(int senderId, int sqlId, string status, string message = "")
        {
            Console.WriteLine($"{senderId} {sqlId} {status} {message}");
            // Отправка ответа на вход
            // ... (ваш код отправки сообщения)
        }

        private void SendRegistrationResponse(int senderId, string status, string message = "")
        {
            Console.WriteLine($"{senderId} {status} {message}");
            // Отправка ответа на регистрацию
            // ... (ваш код отправки сообщения)
        }
    }
}
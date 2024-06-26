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

                int senderId = player.Id;

                try
                {
                    // Определение типа SQL-команды
                    switch (sqlCommand)
                    {
                        case string s when s.StartsWith("Login"):
                            await Task.Run(() => ExecuteLogin(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("Registration"):
                            await Task.Run(() => ExecuteRegistration(sqlCommand, senderId, dbConnection, player));
                            break;
                        case string s when s.StartsWith("GetID"):
                            await Task.Run(() => GetId(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("GetRecepts"):
                            await Task.Run(() => GetRecepts(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("GetRecept"):
                            await Task.Run(() => GetRecept(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("Like"):
                            await Task.Run(() => Like(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("IsLike"):
                            await Task.Run(() => IsLike(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("Dislike"):
                            await Task.Run(() => Dislike(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("IsDislike"):
                            await Task.Run(() => IsDislike(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("Favorite"):
                            await Task.Run(() => Favorite(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("IsFavorite"):
                            await Task.Run(() => IsFavorite(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("GetRecomendsRecepts"):
                            await Task.Run(() => GetRecomendsRecepts(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("GetTags"):
                            await Task.Run(() => GetTags(sqlCommand, senderId, dbConnection, lobby, webSocket));
                            break;
                        case string s when s.StartsWith("SendRecipe"):
                            await Task.Run(() => SendRecipe(sqlCommand, senderId, dbConnection, lobby, webSocket));
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

        private async Task GetRecomendsRecepts(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);

                    // Здесь вы должны использовать ваш запрос SQL
                    cursor.CommandText = $@"
                        SELECT 
                            r.id, 
                            r.title, 
                            r.content, 
                            COALESCE(SUM(CASE WHEN l.action = 'liked' THEN 1 
                                             WHEN l.action = 'disliked' THEN -1 
                                             WHEN l.action = 'favorites' THEN 5 
                                             ELSE 0 END), 0) AS total_score 
                        FROM 
                            recepts r 
                        LEFT JOIN 
                            recept_tags rt ON r.id = rt.recept_id 
                        LEFT JOIN 
                            liked l ON r.id = l.recept_id AND l.user_id = @userId 
                        GROUP BY 
                            r.id, r.title, r.content 
                        ORDER BY 
                            total_score DESC;";

                    cursor.Parameters.AddWithValue("userId", senderId);

                    using (var reader = await cursor.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Здесь вы можете обработать результаты запроса
                            int receptId = reader.GetInt32(0);
                            string title = reader.GetString(1);
                            string content = reader.GetString(2);
                            int totalScore = reader.GetInt32(3);

                            // Ваши действия с полученными данными
                            // Например, отправка сообщений в лобби или куда-либо ещё
                            Console.WriteLine(receptId.ToString() + " / " + totalScore.ToString());
                        }
                    }

                    //lobby.SendMessagePlayer($"/ans true", ws, requestId);
                    Console.WriteLine("true");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error executing GetRecomends command: {e}");
                }
            }
        }

        private async Task SendRecipe(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            List<string> credentials = new List<string>(sqlCommand.Split(' '));
            credentials.RemoveAt(0);
            int requestId = int.Parse(credentials[0]);
            try
            {
                // Parse the sqlCommand to get the requestId, title, and html_content
                string title = credentials[1];
                string htmlContent = credentials[2];

                // Insert the new recipe into the database
                using (var command = dbConnection.CreateCommand())
                {
                    // SQL query to insert the recipe
                    command.CommandText = @"
                INSERT INTO recepts (title, html_content)
                VALUES (@title, @htmlContent);";

                    // Add parameters
                    command.Parameters.AddWithValue("@title", title);
                    command.Parameters.AddWithValue("@htmlContent", htmlContent);

                    // Execute the query and get the inserted recipe's ID
                    command.ExecuteNonQuery();

                    // Construct the message with the new recipe ID
                    string message = $"/ans true";

                    // Send the message to the player
                    lobby.SendMessagePlayer(message, ws, requestId);
                    Console.WriteLine("true");
                }
            }
            catch (Exception e)
            {
                // Log the error
                Console.WriteLine($"Error creating recipe: {e}");

                // Notify of the failure
                lobby.SendMessagePlayer($"/ans false", ws, requestId);
            }
        }


        private async Task GetTags(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            List<string> credentials = new List<string>(sqlCommand.Split(' '));
            credentials.RemoveAt(0);
            int requestId = int.Parse(credentials[0]);
            try
            {
                // Parse the sqlCommand to get the requestId

                // Initialize a list to hold the tag names
                List<string> tagNames = new List<string>();

                using (var cursor = dbConnection.CreateCommand())
                {
                    // SQL query to get the tag names
                    cursor.CommandText = @"
                SELECT 
                    tag 
                FROM 
                    tags;";

                    // Execute the query and process the results
                    using (var reader = await cursor.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Get the tag name
                            string tagName = reader.GetString(0);

                            // Add the tag name to the list
                            tagNames.Add(tagName);
                        }
                    }
                }

                // Construct the message with all tag names
                string message = "/ans true " + string.Join(" ", tagNames);

                // Send the accumulated tag names to the player
                lobby.SendMessagePlayer(message, ws, requestId);
                Console.WriteLine("true");
            }
            catch (Exception e)
            {
                // Log the error
                Console.WriteLine($"Error executing GetTags command: {e}");

                // Notify of the failure
                lobby.SendMessagePlayer($"/ans false", ws, requestId);
            }
        }




        private async Task GetId(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            List<string> credentials = new List<string>(sqlCommand.Split(' '));
            credentials.RemoveAt(0);
            int requestId = int.Parse(credentials[0]);
            lobby.SendMessagePlayer($"/cmdGetID {senderId}", ws, requestId);
        }

        private async Task Dislike(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                using (var transaction = dbConnection.BeginTransaction())
                {
                    try
                    {
                        List<string> credentials = new List<string>(sqlCommand.Split(' '));
                        credentials.RemoveAt(0);
                        int requestId = int.Parse(credentials[0]);
                        int recept_id = int.Parse(credentials[1]);
                        cursor.Parameters.AddWithValue("userId", senderId);
                        cursor.Parameters.AddWithValue("receptId", recept_id);

                        // Проверка на существование записи в таблице liked
                        cursor.CommandText = $"SELECT COUNT(*) FROM liked WHERE user_id = @userId AND recept_id = @receptId";
                        long likedCount = (long)cursor.ExecuteScalar();

                        if (likedCount > 0)
                        {
                            // Если запись существует в таблице liked, удалить ее
                            cursor.CommandText = $"DELETE FROM liked WHERE user_id = @userId AND recept_id = @receptId";
                            cursor.ExecuteNonQuery();
                        }

                        // Проверка на существование записи
                        cursor.CommandText = $"SELECT COUNT(*) FROM disliked WHERE user_id = @userId AND recept_id = @receptId";
                        long count = (long)cursor.ExecuteScalar();

                        if (count > 0)
                        {
                            // Если запись существует, удалить ее
                            cursor.CommandText = $"DELETE FROM disliked WHERE user_id = @userId AND recept_id = @receptId";
                            cursor.ExecuteNonQuery();
                            transaction.Commit();
                            lobby.SendMessagePlayer($"/ans false", ws, requestId);
                        }
                        else
                        {
                            // Если записи не существует, добавить ее
                            cursor.CommandText = $"INSERT INTO disliked(user_id, recept_id) VALUES (@userId, @receptId)";
                            cursor.ExecuteNonQuery();
                            transaction.Commit();
                            lobby.SendMessagePlayer($"/ans true", ws, requestId);
                        }
                    }
                    catch (Exception e)
                    {
                        SendLoginResponse(senderId, -1, "error", "Dislike Not Create");
                        Console.WriteLine($"Error executing Login command: {e}");
                    }
                }
            }
        }

        private async Task IsDislike(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    int recept_id = int.Parse(credentials[1]);
                    cursor.Parameters.AddWithValue("userId", senderId);
                    cursor.Parameters.AddWithValue("receptId", recept_id);

                    // Проверка на существование записи
                    cursor.CommandText = $"SELECT COUNT(*) FROM disliked WHERE user_id = @userId AND recept_id = @receptId";
                    long count = (long)cursor.ExecuteScalar();

                    if (count > 0)
                    {
                        // Если запись существует, отправить сообщение игроку
                        lobby.SendMessagePlayer($"/ans true", ws, requestId);
                        Console.WriteLine("true");
                    }
                    else
                    {
                        // Если записи не существует, отправить сообщение игроку
                        lobby.SendMessagePlayer($"/ans false", ws, requestId);
                        Console.WriteLine("false");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error executing IsDislike command: {e}");
                }
            }
        }



        private async Task Like(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                using (var transaction = dbConnection.BeginTransaction())
                {
                    try
                    {
                        List<string> credentials = new List<string>(sqlCommand.Split(' '));
                        credentials.RemoveAt(0);
                        int requestId = int.Parse(credentials[0]);
                        int recept_id = int.Parse(credentials[1]);
                        cursor.Parameters.AddWithValue("userId", senderId);
                        cursor.Parameters.AddWithValue("receptId", recept_id);

                        // Проверка на существование записи в таблице disliked
                        cursor.CommandText = $"SELECT COUNT(*) FROM disliked WHERE user_id = @userId AND recept_id = @receptId";
                        long dislikedCount = (long)cursor.ExecuteScalar();

                        if (dislikedCount > 0)
                        {
                            // Если запись существует в таблице disliked, удалить ее
                            cursor.CommandText = $"DELETE FROM disliked WHERE user_id = @userId AND recept_id = @receptId";
                            cursor.ExecuteNonQuery();
                        }

                        // Проверка на существование записи
                        cursor.CommandText = $"SELECT COUNT(*) FROM liked WHERE user_id = @userId AND recept_id = @receptId";
                        long count = (long)cursor.ExecuteScalar();

                        if (count > 0)
                        {
                            // Если запись существует, удалить ее
                            cursor.CommandText = $"DELETE FROM liked WHERE user_id = @userId AND recept_id = @receptId";
                            cursor.ExecuteNonQuery();
                            transaction.Commit();
                            lobby.SendMessagePlayer($"/ans false", ws, requestId);
                        }
                        else
                        {
                            // Если записи не существует, добавить ее
                            cursor.CommandText = $"INSERT INTO liked(user_id, recept_id) VALUES (@userId, @receptId)";
                            cursor.ExecuteNonQuery();
                            transaction.Commit();
                            lobby.SendMessagePlayer($"/ans true", ws, requestId);
                        }
                    }
                    catch (Exception e)
                    {
                        SendLoginResponse(senderId, -1, "error", "Like Not Create");
                        Console.WriteLine($"Error executing Login command: {e}");
                    }
                }
            }
        }

        private async Task IsLike(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    int recept_id = int.Parse(credentials[1]);
                    cursor.Parameters.AddWithValue("userId", senderId);
                    cursor.Parameters.AddWithValue("receptId", recept_id);

                    // Проверка на существование записи
                    cursor.CommandText = $"SELECT COUNT(*) FROM liked WHERE user_id = @userId AND recept_id = @receptId";
                    long count = (long)cursor.ExecuteScalar();

                    if (count > 0)
                    {
                        // Если запись существует, отправить сообщение игроку
                        lobby.SendMessagePlayer($"/ans true", ws, requestId);
                        Console.WriteLine("true");
                    }
                    else
                    {
                        // Если записи не существует, отправить сообщение игроку
                        lobby.SendMessagePlayer($"/ans false", ws, requestId);
                        Console.WriteLine("false");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error executing IsLike command: {e}");
                }
            }
        }



        private async Task Favorite(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                using (var transaction = dbConnection.BeginTransaction())
                {
                    try
                    {
                        List<string> credentials = new List<string>(sqlCommand.Split(' '));
                        credentials.RemoveAt(0);
                        int requestId = int.Parse(credentials[0]);
                        int recept_id = int.Parse(credentials[1]);
                        cursor.Parameters.AddWithValue("userId", senderId);
                        cursor.Parameters.AddWithValue("receptId", recept_id);

                        // Check for the existence of the record
                        cursor.CommandText = $"SELECT COUNT(*) FROM favorites WHERE user_id = @userId AND recept_id = @receptId";
                        long count = (long)cursor.ExecuteScalar();

                        if (count > 0)
                        {
                            // If the record exists, delete it
                            cursor.CommandText = $"DELETE FROM favorites WHERE user_id = @userId AND recept_id = @receptId";
                            cursor.ExecuteNonQuery();
                            transaction.Commit();
                            lobby.SendMessagePlayer($"/ans false", ws, requestId);
                        }
                        else
                        {
                            // If the record does not exist, add it
                            cursor.CommandText = $"INSERT INTO favorites(user_id, recept_id) VALUES (@userId, @receptId)";
                            cursor.ExecuteNonQuery();
                            transaction.Commit();
                            lobby.SendMessagePlayer($"/ans true", ws, requestId);
                        }
                    }
                    catch (Exception e)
                    {
                        SendLoginResponse(senderId, -1, "error", "Favorite Not Create");
                        Console.WriteLine($"Error executing Favorite command: {e}");
                    }
                }
            }
        }

        private async Task IsFavorite(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    int recept_id = int.Parse(credentials[1]);
                    cursor.Parameters.AddWithValue("userId", senderId);
                    cursor.Parameters.AddWithValue("receptId", recept_id);

                    // Check for the existence of the record
                    cursor.CommandText = $"SELECT COUNT(*) FROM favorites WHERE user_id = @userId AND recept_id = @receptId";
                    long count = (long)cursor.ExecuteScalar();

                    if (count > 0)
                    {
                        // If the record exists, send a message to the player
                        lobby.SendMessagePlayer($"/ans true", ws, requestId);
                        Console.WriteLine("true");
                    }
                    else
                    {
                        // If the record does not exist, send a message to the player
                        lobby.SendMessagePlayer($"/ans false", ws, requestId);
                        Console.WriteLine("false");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error executing IsFavorite command: {e}");
                }
            }
        }


        private async Task GetRecept(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    int id = int.Parse(credentials[1]);

                    cursor.CommandText = $"SELECT \r\n  recepts.html_content FROM \r\n recepts WHERE recepts.id = {id};";
                    using (NpgsqlDataReader reader = cursor.ExecuteReader())
                    {
                        List<string> data = new List<string>();
                        while (reader.Read())
                        {
                            data.Add(reader["html_content"] == DBNull.Value ? "-" : reader["html_content"].ToString());
                        }

                        string message = string.Join("", data);
                        Console.WriteLine(message);
                        lobby.SendMessagePlayer($"/ans true {message}", ws, requestId);
                    }

                }
                catch (Exception e)
                {
                    SendLoginResponse(senderId, -1, "error", "Recepts Not Found");
                    Console.WriteLine($"Error executing Login command: {e}");
                }
            }
        }

        private async Task GetRecepts(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    int from = int.Parse(credentials[1]), to = int.Parse(credentials[2]);

                    cursor.CommandText = $"SELECT \r\n  recepts.id, \r\n  recepts.title, \r\n  array_agg(DISTINCT products.name) AS products,\r\n  array_agg(DISTINCT users.username) AS users,\r\n  array_agg(DISTINCT tags.tag) AS tags\r\nFROM \r\n  recepts \r\nLEFT JOIN \r\n  recept_products ON recepts.id = recept_products.recept_id \r\nLEFT JOIN \r\n  products ON recept_products.product_id = products.id\r\nLEFT JOIN \r\n  recept_users ON recepts.id = recept_users.recept_id \r\nLEFT JOIN \r\n  users ON recept_users.user_id = users.id\r\nLEFT JOIN \r\n  recept_tags ON recepts.id = recept_tags.recept_id \r\nLEFT JOIN \r\n  tags ON recept_tags.tag_id = tags.id\r\nGROUP BY \r\n  recepts.id;";
                    using (NpgsqlDataReader reader = cursor.ExecuteReader())
                    {
                        List<string> data = new List<string>();
                        while (reader.Read())
                        {
                            data.Add("_text_");

                            data.Add(reader["id"] == DBNull.Value ? "-" : reader["id"].ToString());

                            data.Add(" . ");

                            data.Add("<l>");
                            data.Add(reader["title"] == DBNull.Value ? "-" : reader["title"].ToString());
                            data.Add("</l>");

                            data.Add(" . ");

                            //data.Add(reader["html_content"] == DBNull.Value ? "-" : reader["html_content"].ToString());
                            data.Add(reader["products"] == DBNull.Value ? "-" : String.Join(",", reader["products"].ToString()));

                            data.Add(" . ");

                            data.Add(reader["users"] == DBNull.Value ? "-" : String.Join(",", reader["users"].ToString()));

                            data.Add(" . ");

                            data.Add(reader["tags"] == DBNull.Value ? "-" : String.Join(",", reader["tags"].ToString()));
                        }

                        string message = string.Join("", data);
                        Console.WriteLine(message);
                        lobby.SendMessagePlayer($"/ans true {message}", ws, requestId);
                    }
                }
                catch (Exception e)
                {
                    SendLoginResponse(senderId, -1, "error", "Recepts Not Found");
                    Console.WriteLine($"Error executing Login command: {e}");
                }
            }
        }

        private async Task ExecuteLogin(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Lobby lobby, WebSocket ws)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    // Убираем "Login" из начала SQL-команды
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    string username = credentials[1], password = credentials[2];

                    // Проверка, что пользователь с таким именем существует
                    cursor.CommandText = $"SELECT * FROM users WHERE username='{username}'";
                    using (NpgsqlDataReader reader = cursor.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string storedPassword = reader.GetString(2);
                            string storedSalt = reader.GetString(3);

                            string saltedPassword = password + storedSalt;

                            using (SHA256 sha256 = SHA256.Create())
                            {
                                // Кодируем соленый пароль в байтовую строку перед передачей его объекту хэша
                                byte[] saltedPasswordBytes = Encoding.UTF8.GetBytes(saltedPassword);

                                // Обновляем объект хэша с байтами соленого пароля
                                byte[] hashedPasswordBytes = sha256.ComputeHash(saltedPasswordBytes);

                                // Получаем шестнадцатеричное представление хэша
                                string hashedPassword = BitConverter.ToString(hashedPasswordBytes).Replace("-", "");

                                if (hashedPassword == storedPassword)
                                {
                                    int userId = reader.GetInt32(0);
                                    // Сохраняем id в экземпляре SqlCommander
                                    lobby.Players[ws].Id = userId;
                                    // Вызываем add_player и передаем id
                                    lobby.SendMessageExcept($"Welcome, Player {lobby.Players[ws].Id}", ws);
                                    lobby.SendMessagePlayer($"/ans true", ws, requestId);
                                    SendLoginResponse(senderId, userId, "success");
                                }
                                else
                                {
                                    SendLoginResponse(senderId, -1, "error", "Invalid password");
                                    lobby.SendMessagePlayer($"/ans false", ws, requestId);
                                }
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
        }

        private async Task ExecuteRegistration(string sqlCommand, int senderId, NpgsqlConnection dbConnection, Player player)
        {
            using (var cursor = dbConnection.CreateCommand())
            {
                try
                {
                    List<string> credentials = new List<string>(sqlCommand.Split(' '));
                    credentials.RemoveAt(0);
                    int requestId = int.Parse(credentials[0]);
                    string username = credentials[1], password = credentials[2];

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
        }


        private async Task SendLoginResponse(int senderId, int sqlId, string status, string message = "")
        {
            Console.WriteLine($"{senderId} {sqlId} {status} {message}");
            // Отправка ответа на вход
            // ... (ваш код отправки сообщения)
        }

        private async Task SendRegistrationResponse(int senderId, string status, string message = "")
        {
            Console.WriteLine($"{senderId} {status} {message}");
            // Отправка ответа на регистрацию
            // ... (ваш код отправки сообщения)
        }
    }
}
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public class ChatStatisticsManager
    {
        private readonly string _connectionString;

        // Constructor: Initializes the connection and ensures the table exists
        public ChatStatisticsManager(string connectionString)
        {
            _connectionString = connectionString;
            EnsureTableExists().Wait(); // Ensures the table is created if it doesn't exist
        }

        // Ensures that the table "usermessagestats" exists
        private async Task EnsureTableExists()
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var commandText = @"
                CREATE TABLE IF NOT EXISTS usermessagestats (
                    Username NVARCHAR(100) PRIMARY KEY,  -- The username is the primary key
                    MessageCount INT NOT NULL             -- Tracks the number of messages per user
                );";

                using (var command = new MySqlCommand(commandText, connection))
                {
                    await command.ExecuteNonQueryAsync(); // Executes the SQL statement
                }
            }
        }

        // Loads the message count statistics from the database
        private async Task<List<UserMessageStat>> LoadStatisticsAsync()
        {
            var stats = new List<UserMessageStat>();

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new MySqlCommand("SELECT Username, MessageCount FROM usermessagestats", connection);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        stats.Add(new UserMessageStat
                        {
                            Username = reader.GetString(0),    // Reads the username
                            MessageCount = reader.GetInt32(1) // Reads the message count
                        });
                    }
                }
            }

            return stats; // Returns the list of statistics
        }

        // Saves or updates the message count statistics for a user
        private async Task SaveStatisticsAsync(UserMessageStat stat)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new MySqlCommand(
                    @"INSERT INTO usermessagestats (Username, MessageCount) 
                      VALUES (@Username, @MessageCount)
                      ON DUPLICATE KEY UPDATE MessageCount = @MessageCount", // Updates the message count if the user exists
                    connection);
                command.Parameters.AddWithValue("@Username", stat.Username);    // Passes the username
                command.Parameters.AddWithValue("@MessageCount", stat.MessageCount); // Passes the message count

                await command.ExecuteNonQueryAsync(); // Executes the SQL statement
            }
        }

        // Updates the message count for a user
        public async Task UpdateMessageCountAsync(string username)
        {
            var stats = await LoadStatisticsAsync();

            // Searches for the user in the existing statistics
            var userStat = stats.FirstOrDefault(u => u.Username == username);
            if (userStat != null)
            {
                userStat.MessageCount++; // Increases the message count by 1
            }
            else
            {
                userStat = new UserMessageStat { Username = username, MessageCount = 1 }; // Creates a new user
                stats.Add(userStat);
            }

            await SaveStatisticsAsync(userStat); // Saves the updated data
        }

        // Calculates and returns the statistics
        public async Task<string> GetStatisticsAsync()
        {
            var stats = await LoadStatisticsAsync();

            if (stats.Count == 0) // Checks if there are any statistics
            {
                return "No statistics available.";
            }

            var totalMessages = stats.Sum(u => u.MessageCount);          // Total number of messages
            var avgMessages = (double)totalMessages / stats.Count;       // Average number of messages per user

            // Finds the top three active users
            var topUsers = stats.OrderByDescending(u => u.MessageCount).Take(3)
                .Select(u => $"{u.Username}: {u.MessageCount} messages").ToList();

            // Builds the output string
            var result = new StringBuilder();
            result.AppendLine($"Total number of messages sent: {totalMessages}");
            result.AppendLine($"Average number of messages per user: {avgMessages:F2}");
            result.AppendLine("Top three active users:");
            foreach (var user in topUsers)
            {
                result.AppendLine(user); // Adds each user to the output
            }

            return result.ToString(); // Returns the statistics
        }
    }

    // Class to represent user statistics
    public class UserMessageStat
    {
        public string Username { get; set; }  // Username
        public int MessageCount { get; set; } // Number of messages
    }
}

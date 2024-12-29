using System.Collections.Concurrent;
using System.Drawing;
using Client;
using Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Server;

public class ChatServer
{
    private readonly ConcurrentQueue<ChatMessage> messageQueue = new();
	private readonly ConcurrentDictionary<string, string> userColors = new();
	private readonly List<string> availableColors = new() { "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta" };
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ChatMessage>> waitingClients = new();
    private readonly object lockObject = new();
	private readonly SemaphoreSlim semaphore = new(1, 1);

	// For accessing the WeatherAPI class
	private WeatherAPI api = new WeatherAPI();



	/// <summary>
	/// The function `GenerateUniqueColor` generates a random unique color in hexadecimal format.
	/// </summary>
	/// <returns>
	/// The method `GenerateUniqueColor` returns a randomly generated unique color in hexadecimal format,
	/// prefixed with a `#` symbol.
	/// </returns>
	private string GenerateUniqueColor()
	{
		var random = new Random();
		var color = Color.FromArgb(random.Next(256), random.Next(256), random.Next(256));
		return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
	}


	/// <summary>
	/// The function AssignUniqueColor assigns a unique color to a user based on their username, ensuring
	/// that each user has a distinct color.
	/// </summary>
	/// <param name="username">The `AssignUniqueColor` method takes a `username` as a parameter. This
	/// method is responsible for assigning a unique color to the given `username`. If the `username`
	/// already has a color assigned, it returns the existing color. Otherwise, it assigns a color from
	/// the available colors or generates</param>
	/// <returns>
	/// The method `AssignUniqueColor` returns a string value, which is the unique color assigned to the
	/// specified `username`.
	/// </returns>
	private string AssignUniqueColor(string username)
	{
		lock (lockObject)
		{
			// Check if username already has a color
			if (userColors.TryGetValue(username, out var existingColor))
			{
				return existingColor;
			}

			string assignedColor;

			// Assign from available colors or generate a unique color
			if (availableColors.Count > 0)
			{
				assignedColor = availableColors[0];
				availableColors.RemoveAt(0);
			}
			else
			{
				assignedColor = GenerateUniqueColor();
				// Ensure the generated color is unique
				do
				{
					assignedColor = GenerateUniqueColor();
				} while (userColors.Values.Contains(assignedColor));
			}

			userColors[username] = assignedColor;
			Console.WriteLine($"[INFO] Assigned color '{assignedColor}' to '{username}'");
			return assignedColor;
		}
	}
	public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
			/// <summary>
			/// Handles user registration from the console.
			/// </summary>
			endpoints.MapPost("/console-register", async context =>
			{
				context.Request.Query.TryGetValue("username", out var username);

				if (string.IsNullOrEmpty(username))
				{
					context.Response.StatusCode = StatusCodes.Status400BadRequest;
					await context.Response.WriteAsync("Username is required.");
					return;
				}

				await semaphore.WaitAsync();
				try
				{
					if (userColors.ContainsKey(username))
					{
						context.Response.StatusCode = StatusCodes.Status409Conflict;
						await context.Response.WriteAsync("Username already taken.");
						return;
					}

					string color = AssignUniqueColor(username);

					if (availableColors.Contains(color))
					{
						availableColors.Remove(color);
					}

					userColors[username] = color;
					Console.WriteLine($"[INFO] {username} joined with color {color}");
				}
				finally
				{
					semaphore.Release();
				}

				context.Response.StatusCode = StatusCodes.Status200OK;
				await context.Response.WriteAsync("User registered.");
			});

			/// <summary>
			/// Registers a user and assigns a unique color.
			/// </summary>
			endpoints.MapPost("/register", async context =>
			{
				context.Request.Query.TryGetValue("username", out var username);

				if (string.IsNullOrEmpty(username))
				{
					context.Response.StatusCode = StatusCodes.Status400BadRequest;
					await context.Response.WriteAsync("Username is required.");
					return;
				}

				await semaphore.WaitAsync();

				try
				{
					if (waitingClients.ContainsKey(username))
					{
						context.Response.StatusCode = StatusCodes.Status409Conflict;
						await context.Response.WriteAsync("Username already taken.");
						return;
					}

					string assignedColor = AssignUniqueColor(username);
					waitingClients.TryAdd(username, new TaskCompletionSource<ChatMessage>());
					context.Response.StatusCode = StatusCodes.Status200OK;
					await context.Response.WriteAsync(assignedColor);

				}
				finally
				{
					semaphore.Release();
				}
			});
			// GET: /messages
			endpoints.MapGet("/messages", async context =>
            {
                var tcs = new TaskCompletionSource<ChatMessage>();

                context.Request.Query.TryGetValue("id", out var rawId);
                var id = rawId.ToString();

                Console.WriteLine($"Client '{id}' registered");

                var error = true;
                lock (this.lockObject)
                {
                    if (this.waitingClients.ContainsKey(id))
                    {
                        if (this.waitingClients.TryRemove(id, out _))
                        {
                            Console.WriteLine($"Client '{id}' removed from waiting clients");
                        }
                    }

                    if (this.waitingClients.TryAdd(id, tcs))
                    {
                        Console.WriteLine($"Client '{id}' added to waiting clients");
                        error = false;
                    }
                }

                if (error)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsync("Internal server error.");
                    return;
                }

                var message = await tcs.Task;

                Console.WriteLine($"Client '{id}' received message: {message.Content}");

                await context.Response.WriteAsJsonAsync(message);
            });

            // POST: /messages
            endpoints.MapPost("/messages", async context =>
            {
                var message = await context.Request.ReadFromJsonAsync<ChatMessage>();

                if (message == null)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Message invalid.");
                    return;
                }

                Console.WriteLine($"Received message from client: {message.Content}");

                if (message.Content.StartsWith("/"))
                {
                    if (message.Content.StartsWith("/wetter"))
                    {
                        string address = message.Content.Substring("/wetter".Length).Trim();

                        if (!string.IsNullOrEmpty(address))
                        {
                            string result = await api.GetWeather(address, 0);

                            if (result.StartsWith("retry"))
                            {
                                result = result.Substring("retry".Length).Trim();
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                await context.Response.WriteAsync(
                                    $"Welches {address} meinen Sie? Schreiben Sie bitte eine Ziffer aus der Liste.\n{result}");
                            }
                            else
                            {
                                context.Response.StatusCode = StatusCodes.Status200OK;
                                await context.Response.WriteAsync(
                                    $"Sehr gerne gebe ich Ihnen das Wetter für \"{address}\" aus");
                                await context.Response.WriteAsync(result);
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            await context.Response.WriteAsync(
                                "Es wurde keine Adresse angegeben. Bitte verwenden Sie den Befehl folgendermaßen: /wetter <adress>");
                        }
                    }
                }
                else
                {
					lock (lockObject)
					{
						message.Color = userColors.GetValueOrDefault(message.Sender, "White");
						message.Timestamp = DateTime.Now;

						if (!string.IsNullOrEmpty(message.Recipient))
						{
							if (waitingClients.TryGetValue(message.Recipient, out var recipientClient))
							{
								recipientClient.TrySetResult(message);
							}
							else
							{
								if (waitingClients.TryGetValue(message.Sender, out var senderClient))
								{
									var errorMessage = new ChatMessage
									{
										Sender = "System",
										Content = $"Recipient '{message.Recipient}' not found.",
										Color = "Red",
										Timestamp = DateTime.Now
									};
									senderClient.TrySetResult(errorMessage);
									waitingClients.TryRemove(message.Sender, out _);
								}
							}
						}
						else
						{
							foreach (var (clientId, clientTask) in waitingClients.ToArray())
							{
								if (clientId != message.Sender)
								{
									clientTask.TrySetResult(message);
								}
							}
						}
					}

					context.Response.StatusCode = StatusCodes.Status201Created;
                    await context.Response.WriteAsync("Message received and processed.");
                }
            });

            app.UseEndpoints(endpoints =>
            {
                // Endpoint for GET requests to "/statistics"
                endpoints.MapGet("/statistics", async context =>
                {
                    // Create an instance of ChatStatisticsManager with the database connection string
                    var statisticsManager =
                        new ChatStatisticsManager("Server=localhost;Database=chatbot;User Id=root;Password=;");

                    // Asynchronously retrieve statistics from the database
                    var result = await statisticsManager.GetStatisticsAsync();

                    // Set the HTTP status code to 200 (OK) and send the statistics as a response
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsync(result);
                });

                // Endpoint for POST requests to "/statistics"
                endpoints.MapPost("/statistics", async context =>
                {
                    // Read the username from the JSON body of the request
                    var username = await context.Request.ReadFromJsonAsync<string>();

                    // Check if the username is invalid (e.g., null or empty)
                    if (string.IsNullOrEmpty(username))
                    {
                        // If invalid, set the status code to 400 (Bad Request) and send an error message
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid username.");
                        return;
                    }

                    // Create an instance of ChatStatisticsManager with the database connection string
                    var statisticsManager =
                        new ChatStatisticsManager("Server=localhost;Database=chatbot;User Id=root;Password=;");

                    // Asynchronously update the message count for the specified user in the database
                    await statisticsManager.UpdateMessageCountAsync(username);

                    // Set the HTTP status code to 200 (OK) and send a success message
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsync("Statistics updated.");
                });
				/// <summary>
				/// Retrieves a list of currently connected clients excluding the requesting client.
				/// </summary>
				endpoints.MapGet("/clients", async context =>
				{
					context.Request.Query.TryGetValue("id", out var currentClient);
					List<string> clients;

					lock (lockObject)
					{
						clients = waitingClients.Keys.Where(client => client != currentClient).ToList();
					}

					await context.Response.WriteAsJsonAsync(clients);
				});
			});
        });
    }
}

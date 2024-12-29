using System;
using System.Text;
using System.Threading.Tasks;
using Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Client
{
    /// <summary>
    /// A most basic chat client for the console
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var serverUri = new Uri("http://localhost:5000");
            // Query the user for a name
            Console.Write("Enter your name: ");
            var sender = Console.ReadLine() ?? Guid.NewGuid().ToString();
            Console.WriteLine();
            
            using var httpClient = new HttpClient() { BaseAddress = serverUri };
			var messagePayload = new StringContent($"\"{sender}\"", Encoding.UTF8, "application/json");
            await httpClient.PostAsync("http://localhost:5000/statistics", messagePayload);
			try
			{
				
				var response = await httpClient.PostAsync($"/register?username={Uri.EscapeDataString(sender)}", null);

				if (response.IsSuccessStatusCode)
				{
					var assignedColor = await response.Content.ReadAsStringAsync();
					Console.ForegroundColor = ConsoleColor.White;
					Console.WriteLine($"User '{sender}' successfully registered with color: {assignedColor}");

					// Display join message with assigned color
					Console.ForegroundColor = ConsoleColor.White;
					Console.WriteLine($"{sender} has joined the chat!");
				}
				else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("Username already taken. Please choose another.");
					Environment.Exit(1); // Terminate if username is taken
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("Error during registration. Try again.");
					Environment.Exit(1); // Terminate on registration failure
				}
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Registration failed: {ex.Message}");
				Environment.Exit(1); // Terminate on connection failure
			}
			finally
			{
				Console.ResetColor();
			}

			// Create a chat client
			var client = new ChatClient(sender, serverUri);
            client.MessageReceived += MessageReceivedHandler;

            // Connect to the server and start listening for messages
            var connectTask = await client.Connect();
            var listenTask = client.ListenForMessages();

            // Query the user for messages to send or the 'exit' command
            while (true)
            {
                Console.Write("Enter your message (or 'exit' to quit): ");
                var content = Console.ReadLine() ?? string.Empty;

                // Exit if 'exit' is typed
                if (content.ToLower() == "exit")
                {
                    client.CancelListeningForMessages();
                    break;
                }

                // Check if a command was typed
                if (content.StartsWith("/"))
                {
					// Checks if the user enters a message with the prefix '/private'
	                if (content.StartsWith("/private"))
	                {
		                // Parses the input by splitting it into three parts:
		                // The first part is the command (/private),
		                // the second part is the recipient's name,
		                // the third part is the actual message.
		                var parts = content.Split(' ', 3);
		                // Checks if all three parts are present correctly.
		                if (parts.Length >= 3)
		                {
			                var recipient = parts[1]; // The recipient of the private message
			                var privateMessage = parts[2]; // The content of the private message
			                if (await client.SendPrivateMessage(recipient, privateMessage))
			                {
				                // Successfully sent the private message.
				                Console.WriteLine("Private message sent successfully.");
			                }
			                else
			                {
				                // Failed to send the private message.
				                Console.WriteLine("Failed to send private message.");
			                }
		                }
		                else
		                {
			                // Shows the user the correct usage of the command.
			                Console.WriteLine("Usage: /private <recipient> <message>");
		                }
	                }
					// Client-side code for retrieving statistics
	                else if (content.StartsWith("/statistik"))
	                {
		                try
		                {
			                var response = await httpClient.GetStringAsync("http://localhost:5000/statistics"); // Retrieves statistics from the server
			                Console.WriteLine("Statistics:");
			                Console.WriteLine(response); // Outputs the statistics
		                }
		                catch (HttpRequestException ex)
		                {
			                Console.WriteLine($"Error retrieving statistics: {ex.Message}"); // Error message in case of a connection issue
		                }
	                }
                    //weather command
                    else if (content.StartsWith("/wetter"))
                    {
                        //send the weather command to the server to get the weather or further instructions
						Response resp = await client.SendWeatherMessage(content);

                        // check if there is a return message
						if (resp.Message != null && resp.Message != "")
						{
                            //if it starts with "Weleches [...]" it indecates the decision part. The user have to choose between differend adresses
							if (resp.Message.StartsWith("Welches"))
							{

								string input;
								int number;
								bool isInList = false;
								do
								{
									do
									{
                                        //Clearing console for better view
										Console.Clear();
                                        //display the Message from the sever (the different adresses)
										Console.WriteLine(resp.Message);

                                        // ask user to give the correct number back
										Console.WriteLine("Bitte geben Sie eine Nummer ein:");
										input = Console.ReadLine();

                                        //Check if input is a number
										int.TryParse(input, out number);

									}
									while (number == 0);// as long as number is not overwritten, repeat the input part

									//splitting the Rows to separate the possible choices
									string[] lines = resp.Message.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                                    // checking every line 
									foreach (string line in lines)
									{
                                        // check if the line is the one that the users number represents
										if (line.StartsWith($"[{input}]"))
										{
                                            //when number matches with the line the adress is filtered out of the line and send with the weather command: /wetter <address>
											string selectedAddress = line.Substring(line.IndexOf(']') + 1).Trim();
											Response resp2 = await client.SendWeatherMessage("/wetter " + selectedAddress);
                                            
                                            //cleaning the console to get a better view on the weatherdata
											Console.Clear();
                                            //display the weather data
											Console.WriteLine(resp2.Message);

                                            //setting the variable true to show the outer do-while loop that the line was found and the weather command ends
											isInList = true;
										}
									}


								}
								while (!isInList); // as long as the line was not found the input process repeats itselfe
							}
                            //The users input was detailed enough so that only one adress was found. So the system will display the weather
							if (resp.Message.StartsWith("Sehr"))
							{
                                //Clearing the console for a better view on the weather data
								Console.Clear();
                                //show weather data
								Console.WriteLine(resp.Message);
							}
						}
					}
                }
                else
                {
                    // if there isn't a command given by the user-> it is handled as a message

                    Console.WriteLine($"Sending message: {content}");

                    if (await client.SendMessage(content))
                    {
	                    Console.WriteLine("Message sent successfully.");

	                    // Update statistics
	                    messagePayload = new StringContent($"\"{sender}\"", Encoding.UTF8, "application/json");
	                    await httpClient.PostAsync("http://localhost:5000/statistics", messagePayload);
                    }
                    else
                    {
	                    Console.WriteLine("Failed to send message.");
                    }

                }
            }

            // Wait for the listening task to end
            await Task.WhenAll(listenTask);

            Console.WriteLine("\nGood bye...");
        }

        /// <summary>
        /// Helper method to display the newly received messages.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MessageReceivedEventArgs"/> instance containing the event data.</param>
        static void MessageReceivedHandler(object? sender, MessageReceivedEventArgs e)
        {
            Console.WriteLine($"\nReceived new message from {e.Sender}: {e.Message}");
        }
    }
}

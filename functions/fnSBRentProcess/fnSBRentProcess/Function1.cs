using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace fnSBRentProcess;

public class ProcessaLocacao
{
    private readonly ILogger<ProcessaLocacao> _logger;
    private readonly IConfiguration _configuration;

    public ProcessaLocacao(ILogger<ProcessaLocacao> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function(nameof(ProcessaLocacao))]
    public async Task Run(
        [ServiceBusTrigger("queue-locacoes", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        var body = message.Body.ToString();
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

        RentModel rentModel = null;
        try
        {
            rentModel = JsonSerializer.Deserialize<RentModel>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (rentModel is null)
            {
                _logger.LogError("Deserialized RentModel is null.");
                await messageActions.DeadLetterMessageAsync(message, null, "RentModel is null after deserialization.");
                return;
            }
           
            var connectionString = _configuration.GetConnectionString("SqlConnectionString");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var command = new SqlCommand(@"INSERT INTO LOCACAO (Nome, Email, Modelo, Ano, TempoAluguel, Data) VALUES (@Nome, @Email, @Modelo, @Ano, @TempoAluguel, @Data)", connection);
            command.Parameters.AddWithValue("@Nome", rentModel.nome);
            command.Parameters.AddWithValue("@Email", rentModel.email);
            command.Parameters.AddWithValue("@Modelo", rentModel.modelo);
            command.Parameters.AddWithValue("@Ano", rentModel.ano);
            command.Parameters.AddWithValue("@TempoAluguel", rentModel.tempoAluguel);
            command.Parameters.AddWithValue("@Data", rentModel.data);
            
            var serviceBusConnection = _configuration.GetSection("ServiceBusConnection").Value.ToString();
            var serviceBusQueueName = _configuration.GetSection("ServiceBusQueue").Value.ToString();    
            sendMessageToPlay(serviceBusConnection, serviceBusQueueName, rentModel);

            var rowsAffected = await command.ExecuteNonQueryAsync();

            connection.Close();
            // Complete the message
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing message: {messageId}, Error: {error}", message.MessageId, ex.Message);
            await messageActions.DeadLetterMessageAsync(message, null, "Error processing message: " + ex.Message);
            return;
        }




        
    }

    private void sendMessageToPlay(string serviceBusConnection, string serviceBusQueueName, RentModel rentModel)
    {
        ServiceBusClient client = new ServiceBusClient(serviceBusConnection);
        ServiceBusSender sender = client.CreateSender(serviceBusQueueName);

        ServiceBusMessage message = new ServiceBusMessage(JsonSerializer.Serialize(rentModel));
        message.ContentType = "application/json";
        message.ApplicationProperties.Add("Nome", rentModel.nome);
        message.ApplicationProperties.Add("Email", rentModel.email);
        message.ApplicationProperties.Add("Modelo", rentModel.modelo);
        message.ApplicationProperties.Add("Ano", rentModel.ano);
        message.ApplicationProperties.Add("TempoAluguel", rentModel.tempoAluguel);
        message.ApplicationProperties.Add("Data", rentModel.data.ToString("o")); // ISO 8601 format
        message.ApplicationProperties.Add("MessageType", "RentModel");
        message.ApplicationProperties.Add("MessageId", Guid.NewGuid().ToString());
        message.ApplicationProperties.Add("MessageTimestamp", DateTime.UtcNow.ToString("o")); // ISO 8601 format
        sender.SendMessageAsync(message).Wait();
        sender.DisposeAsync();    
       
    }
}
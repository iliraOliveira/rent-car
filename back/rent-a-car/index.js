const express = require('express');
const cors = require('cors');
const { DefaultAzureCredential } = require('@azure/identity');
const { ServiveBusClient, ServiceBusClient } = require('@azure/service-bus');
require('dotenv').config();
const app = express();
app.use(cors());
app.use(express.json());

app.post('/api/locacao', async (req, res) => {
    const { nome, email, modelo, ano, tempoAluguel } = req.body;
    const connectionString = "";

    const mensagem = {
        nome,
        email,
        modelo,
        ano,
        tempoAluguel,
        data: new Date().toISOString(),
    };

    try {
        const credential = new DefaultAzureCredential();
        const serviceBusConnection = connectionString; // Use the connection string directly
        const queueName = 'queue-locacoes';
        const serviceBusClient = new ServiceBusClient(serviceBusConnection);
        const sender = serviceBusClient.createSender(queueName);
        const message = {
            body: mensagem,
            contentType: 'application/json',
        };
        await sender.sendMessages({ body: mensagem });
        await sender.close();
        await serviceBusClient.close();
        res.status(201).json({ message: 'Locação processada com sucesso!' });

    } catch (error) {
        console.error('Erro ao enviar mensagem:', error);
        res.status(500).json({ error: 'Erro ao processar a locação.' });
    }
})

app.listen(3001, () => {
    console.log('Servidor rodando na porta 3001');
});
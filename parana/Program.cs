using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

class Program
{
    private static string tokenNew = string.Empty;
    private static DateTime tokenLastGenerated = DateTime.MinValue;
    private static readonly TimeSpan tokenValidityPeriod = TimeSpan.FromMinutes(5);

    static async Task Main(string[] args)
    {
        while (true)
        {
            try
            {
                await ProcessJobsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no sistema: {ex.Message}");
                await Task.Delay(5000);
            }
        }
    }

    static async Task ProcessJobsAsync()
    {
        using (var cnx = new MySqlConnection("Server=dado_ocultado;Database=dado_ocultado;Uid=dado_ocultado;Pwd=dado_ocultado;"))
        {
            await cnx.OpenAsync();
            using (var cmd = cnx.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM jobs_fgts WHERE sit=0 AND campanha_fgts=1";
                var result = await cmd.ExecuteScalarAsync();
                if (result == DBNull.Value || Convert.ToInt32(result) == 0)
                {
                    Console.WriteLine("Sem base para consultar...");
                    await Task.Delay(30000);
                    return;
                }

                string codigoTabela = null;
                int? idUsers = null;

                cmd.CommandText = "SELECT id_usuario, cod_tabela FROM jobs_fgts WHERE sit=0 AND campanha_fgts=1";
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        idUsers = reader.GetInt32(0);
                        codigoTabela = reader.GetString(1);
                    }
                }

                if (!string.IsNullOrEmpty(codigoTabela))
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM parana_dbtemp.{codigoTabela} WHERE sit IS NULL";
                    result = await cmd.ExecuteScalarAsync();
                    if (result == DBNull.Value || Convert.ToInt32(result) == 0)
                    {
                        cmd.CommandText = $"UPDATE jobs_fgts SET sit=1 WHERE cod_tabela='{codigoTabela}'";
                        await cmd.ExecuteNonQueryAsync();
                        await Task.Delay(2000);
                    }
                    else
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            await EnviarUnicoAsync(idUsers.Value, codigoTabela, cnx);
                            await Task.Delay(2000);
                        }
                    }
                }
            }
        }
    }

    static async Task EnviarUnicoAsync(int idUser, string codigoTabela, MySqlConnection cnx)
    {
        try
        {
            using (var cmd = cnx.CreateCommand())
            {
                cmd.CommandText = $"SELECT cpf, var2 FROM parana_dbtemp.{codigoTabela} WHERE sit IS NULL ORDER BY RAND() LIMIT 1";
                using (var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow))
                {
                    if (!await reader.ReadAsync()) return;

                    string cpfClienteParana = reader.GetString(0);
                    string dtNascimento = reader.GetString(1);
                    reader.Close();

                    dtNascimento = FormatDate(dtNascimento);
                    cpfClienteParana = SanitizeCpf(cpfClienteParana);

                    tokenNew = await EnsureValidTokenAsync();

                    var saldoData = await FetchSaldoAsync(cpfClienteParana);
                    if (saldoData == null) return;

                    await UpdateSaldoAsync(cmd, codigoTabela, cpfClienteParana, saldoData.saldoTotal.Value);

                    var simulacaoData = await FetchSimulacaoAsync(cpfClienteParana, dtNascimento, saldoData);
                    if (simulacaoData == null) return;

                    await UpdateSimulacaoAsync(cmd, codigoTabela, cpfClienteParana, simulacaoData.valorLiberado.Value);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
        }
    }

    static async Task<string> EnsureValidTokenAsync()
    {
        if (string.IsNullOrEmpty(tokenNew) || DateTime.UtcNow - tokenLastGenerated >= tokenValidityPeriod)
        {
            tokenNew = await ObterTokenAsync();
            tokenLastGenerated = DateTime.UtcNow;
            Console.WriteLine("Cabeçalhos da solicitação:");
            Console.WriteLine($"Authorization: Bearer {tokenNew}");
        }
        return tokenNew;
    }

    static async Task<dynamic> FetchSaldoAsync(string cpfClienteParana)
    {
        using (var httpClient = new HttpClient())
        {
            var saldoBody = new
            {
                cpf = cpfClienteParana,
                quantidadeDePeriodos = 7
            };
            var saldoJson = JsonConvert.SerializeObject(saldoBody);
            var saldoContent = new StringContent(saldoJson, Encoding.UTF8, "application/json");

            // Adicionar os cabeçalhos
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var token = await EnsureValidTokenAsync();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Console.WriteLine("Conteúdo da solicitação:");
            Console.WriteLine(saldoJson);

            try
            {
                var saldoResponse = await httpClient.PostAsync("https://api-marketplace.dado_ocultado/v1/fgts/saque-aniversario/saldo-disponivel", saldoContent);

                if (!saldoResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro na solicitação HTTP: {saldoResponse.StatusCode}");
                    var errorContent = await saldoResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Conteúdo do erro: {errorContent}");
                    return null;
                }

                var saldoText = await saldoResponse.Content.ReadAsStringAsync();

                if (saldoResponse.Content.Headers.ContentType.MediaType == "application/json")
                {
                    dynamic saldoData = JsonConvert.DeserializeObject(saldoText);
                    if (saldoData == null || saldoData.erros != null)
                    {
                        Console.WriteLine("Erro ao obter dados do saldo.");
                        return null;
                    }
                    return saldoData;
                }
                Console.WriteLine("Erro na resposta do saldo.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar solicitação HTTP: {ex.Message}");
                return null;
            }
        }
    }

    static async Task UpdateSaldoAsync(MySqlCommand cmd, string codigoTabela, string cpfClienteParana, decimal saldoTotal)
    {
        cmd.CommandText = $"UPDATE parana_dbtemp.{codigoTabela} SET saldo_fgts='{saldoTotal}' WHERE cpf='{cpfClienteParana}'";
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task<dynamic> FetchSimulacaoAsync(string cpfClienteParana, string dtNascimento, dynamic saldoData)
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await EnsureValidTokenAsync());

            var simulacaoBody = new
            {
                pessoaId = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                cpf = cpfClienteParana,
                dataDeNascimento = $"{dtNascimento}T00:00:00.000Z",
                dataDeCalculo = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}",
                saldoDisponivel = saldoData.saldoTotal.Value,
                tipoDeSimulacaoSaqueAniversario = "SaldoDisponivel",
                quantidadeDeParcelas = 7,
                saldosPorPeriodos = saldoData.saldosPorPeriodos,
                codigoDaRegra = "string"
            };
            var simulacaoJson = JsonConvert.SerializeObject(simulacaoBody);
            var simulacaoContent = new StringContent(simulacaoJson, Encoding.UTF8, "application/json");
            var simulacaoResponse = await httpClient.PostAsync("https://api-marketplace.dado_ocultado/v1/fgts/saque-aniversario/simulacao", simulacaoContent);
            var simulacaoText = await simulacaoResponse.Content.ReadAsStringAsync();

            if (simulacaoResponse.Content.Headers.ContentType.MediaType == "application/json")
            {
                dynamic simulacaoData = JsonConvert.DeserializeObject(simulacaoText);
                if (simulacaoData.erros != null)
                {
                    Console.WriteLine($"Erro na simulação: {simulacaoData.erros[0].mensagem}");
                    return null;
                }
                return simulacaoData;
            }
            Console.WriteLine($"Erro na simulação: {simulacaoText}");
            return null;
        }
    }

    static async Task UpdateSimulacaoAsync(MySqlCommand cmd, string codigoTabela, string cpfClienteParana, decimal valorLiberado)
    {
        cmd.CommandText = $"UPDATE parana_dbtemp.{codigoTabela} SET sit=1, saldo_fgts='{valorLiberado}', saldo_liberado='{valorLiberado}', dt_consulta='{DateTime.Now:yyyy-MM-dd HH:mm:ss}' WHERE cpf='{cpfClienteParana}'";
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"Sucesso | CPF: {cpfClienteParana}");
    }

    static async Task<string> ObterTokenAsync()
    {
        using (var httpClient = new HttpClient())
        {
            var tokenBody = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_secret", "dado_ocultado" },
                { "client_id", "dado_ocultado" }
            };
            var tokenContent = new FormUrlEncodedContent(tokenBody);
            var tokenResponse = await httpClient.PostAsync("https://api-marketplace.dado_ocultado/v1/auth/token", tokenContent);
            var tokenText = await tokenResponse.Content.ReadAsStringAsync();

            Console.WriteLine($"Resposta do token: {tokenText}");
            if (tokenResponse.Content.Headers.ContentType?.MediaType == "application/json")
            {
                dynamic tokenData = JsonConvert.DeserializeObject(tokenText);
                string tokenNew = tokenData.access_token;
                Console.WriteLine("NOVO TOKEN GERADO");
                return tokenNew;
            }
            Console.WriteLine($"Erro ao obter token: {tokenText}");
            throw new Exception("Falha ao obter token");
        }
    }

    static string FormatDate(string dt)
    {
        return dt.Substring(6) + '-' + dt.Substring(3, 2) + '-' + dt.Substring(0, 2);
    }

    static string SanitizeCpf(string cpf)
    {
        return cpf.Replace("(", string.Empty).Replace(")", string.Empty).Replace("'", string.Empty);
    }
}

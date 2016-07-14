using System;
using System.IO;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Npgsql;
using StackExchange.Redis;

namespace Worker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var pgsql = OpenDbConnection("Server=db;Username=postgres;");
                var redis = OpenRedisConnection("redis").GetDatabase();

                CreateOptions(pgsql, redis);

                var definition = new { vote = "", voter_id = "" , vote_date = ""};
                while (true)
                {
                    string json = redis.ListLeftPopAsync("votes").Result;
                    if (json != null)
                    {
                        var vote = JsonConvert.DeserializeAnonymousType(json, definition);
                        Console.WriteLine($"Processing vote for '{vote.vote}' by '{vote.voter_id}' at '{vote.vote_date}'");
                        UpdateVote(pgsql, vote.voter_id, vote.vote_date, vote.vote);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static void CreateOptions(NpgsqlConnection pgsql, IDatabase redis)
        {
            NpgsqlDataReader dr = RunCommandQuery(pgsql, "SELECT name FROM options");

            List<string> options = new List<string>();
            while(dr.Read())
            {
                Console.WriteLine("Record: "+dr[0].ToString());

                options.Add(dr.GetString(0));
            }

            Console.WriteLine("Array: "+ String.Join(", ", options.ToArray()));

            redis.KeyDelete("options");
            redis.ListRightPush("options", "camarada");
            redis.ListRightPush("options", "outback");
            redis.ListRightPush("options", "alphaiate");
        }

        private static NpgsqlConnection OpenDbConnection(string connectionString)
        {
            NpgsqlConnection connection;

            while (true)
            {
                try
                {
                    connection = new NpgsqlConnection(connectionString);
                    connection.Open();
                    break;
                }
                catch (SocketException)
                {
                    Console.Error.WriteLine("Waiting for db");
                    Thread.Sleep(1000);
                }
                catch (DbException)
                {
                    Console.Error.WriteLine("Waiting for db");
                    Thread.Sleep(1000);
                }
            }

            Console.Error.WriteLine("Connected to db");

            CreateTableVotes(connection);
            CreateTableOptions(connection);


            return connection;
        }

        private static void CreateTableVotes(NpgsqlConnection connection)
        {
            RunCommandNoQuery(connection, @"CREATE TABLE IF NOT EXISTS votes (
                                        id VARCHAR(255) NOT NULL UNIQUE, 
                                        date DATE NOT NULL,
                                        vote VARCHAR(255) NOT NULL
                                    )");

        }
        
        private static void CreateTableOptions(NpgsqlConnection connection)
        {
            try {
                RunCommandNoQuery(connection, "SELECT * FROM options where 1 = 0");
            }
            catch(DbException)
            {
                RunCommandNoQuery(connection, @"CREATE TABLE IF NOT EXISTS options (
                                            id SERIAL PRIMARY KEY,
                                            name VARCHAR(255) NOT NULL
                                        )");

                string[] options = File.ReadAllLines("options.txt");
                foreach (string s in options)
                {
                    RunCommandNoQuery(connection, String.Format("INSERT INTO options (name) values ('{0}')",s));
                }
            }
        }

        private static void RunCommandNoQuery(NpgsqlConnection connection, string CommandText)
        {
            var command = connection.CreateCommand();
            command.CommandText = CommandText;
            command.ExecuteNonQuery();
        }

        private static NpgsqlDataReader RunCommandQuery(NpgsqlConnection connection, string CommandText)
        {
            var command = connection.CreateCommand();
            command.CommandText = CommandText;
            return command.ExecuteReader();
        }

        private static ConnectionMultiplexer OpenRedisConnection(string hostname)
        {
            // Use IP address to workaround hhttps://github.com/StackExchange/StackExchange.Redis/issues/410
            var ipAddress = GetIp(hostname);
            Console.WriteLine($"Found redis at {ipAddress}");

            while (true)
            {
                try
                {
                    Console.Error.WriteLine("Connected to redis");
                    return ConnectionMultiplexer.Connect(ipAddress);
                }
                catch (RedisConnectionException)
                {
                    Console.Error.WriteLine("Waiting for redis");
                    Thread.Sleep(1000);
                }
            }
        }

        private static string GetIp(string hostname)
            => Dns.GetHostEntryAsync(hostname)
                .Result
                .AddressList
                .First(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToString();

        private static void UpdateVote(NpgsqlConnection connection, string voterId, string voteDate, string vote)
        {
            var command = connection.CreateCommand();
            try
            {
                command.CommandText = "INSERT INTO votes (id, date, vote) VALUES (@id, to_date(@date,'DD/MM/YYYY'), @vote)";
                command.Parameters.AddWithValue("@id", voterId);
                command.Parameters.AddWithValue("@date", voteDate);
                command.Parameters.AddWithValue("@vote", vote);
                command.ExecuteNonQuery();
            }
            catch (DbException)
            {
                command.CommandText = "UPDATE votes SET vote = @vote WHERE id = @id and date = to_date(@date,'DD/MM/YYYY')";
                command.ExecuteNonQuery();
            }
            finally
            {
                command.Dispose();
            }
        }
    }
}
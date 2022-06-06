using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;

namespace BaseDB
{
    class BaseDBClass
    {
        public string ConnectionString, Message;
        public SqlTransaction Tran = null;
        public SqlConnection Conn = null;
        public SqlDataReader QueryResult = null;

        private SqlCommand Cmd = null;

        public BaseDBClass(string Server, string Base, string User)
        {
            ConnectionString = string.Format("data source={0};initial catalog={1};user id={2};Password=", Server, Base, User);
            Conn = new SqlConnection();
            Message = "";
        }

        public int Open(string Pwd)
        {
            Message = "";
            if (Conn.State == ConnectionState.Open)
                Close();
            Conn.ConnectionString = ConnectionString + Pwd;
            try
            {
                Conn.Open();
                if (Conn.State != ConnectionState.Open)
                {
                    Message = "BaseDB: Cannot connect to the database";
                    return -2;
                }
            }
            catch (Exception ex)
            {
                Message = "BaseDB: " + ex.Message;
                return -1;
            }
            return 0;
        }

        public int Close()
        {
            Message = "";
            try
            {
                Conn.Close();
            }
            catch (Exception ex)
            {
                Message = "BaseDB: " + ex.Message;
                return -1;
            }
            return 0;
        }

        public int RunExec(string Query, Dictionary<string, string[]> Params, out string Status)
        {
            Status = ""; 

            try
            {
                if (QueryResult != null)
                {
                    if (!QueryResult.IsClosed)
                        QueryResult.Close();
                }

                if (Tran != null)
                    Cmd = new SqlCommand(Query, Conn, Tran);
                else
                    Cmd = new SqlCommand(Query, Conn);

                Cmd.CommandTimeout = 0;
                Cmd.CommandType = CommandType.StoredProcedure;

                SqlParameter output = new SqlParameter();

                foreach (string key in Params.Keys)
                {
                    string[] p = Params[key];
                    string value = p[0];
                    string direction = p.Length > 1 ? p[1] : "input";
                    string dbtype = p.Length > 2 ? p[2] : "string";

                    SqlParameter item = new SqlParameter();

                    item.ParameterName = "@p_" + key;
                    switch (dbtype)
                    {
                        case "int":
                            item.SqlDbType = SqlDbType.Int;
                            break;
                        case "float":
                            item.SqlDbType = SqlDbType.Float;
                            break;
                        default:
                            item.SqlDbType = SqlDbType.VarChar;
                            break;
                    }
                    switch (direction)
                    {
                        case "inputoutput":
                            item.Direction = ParameterDirection.InputOutput;
                            output = item;
                            break;
                        case "output":
                            item.Direction = ParameterDirection.Output;
                            output = item;
                            break;
                        default:
                            item.Direction = ParameterDirection.Input;
                            break;
                    }
                    item.Value = value;

                    Cmd.Parameters.Add(item);
                }

                Cmd.ExecuteNonQuery();
                
                if (output != null)
                    Status = output.Value.ToString();
            }
            catch (Exception ex)
            {
                Message = string.Format("BaseDB.RunStoredProcedure: {0}. {1}", Query, ex.Message);
                return -1;
            }
            return 0;
        }

        public int RunQuery(string Query)
        {
            try
            {
                if (QueryResult != null)
                {
                    if (!QueryResult.IsClosed)
                        QueryResult.Close();
                }

                if (Tran != null)
                    Cmd = new SqlCommand(Query, Conn, Tran);
                else
                    Cmd = new SqlCommand(Query, Conn);

                Cmd.CommandTimeout = 0;
                QueryResult = Cmd.ExecuteReader();
                if (QueryResult.HasRows)
                    return 1;
            }
            catch (Exception ex)
            {
                Message = string.Format("BaseDB.RunQuery: {0}. {1}", Query, ex.Message);
                return -1;
            }
            return 0;
        }

        public int RunCommand(string Command)
        {
            try
            {
                if (QueryResult != null)
                {
                    if (!QueryResult.IsClosed)
                        QueryResult.Close();
                }

                if (Tran != null)
                    Cmd = new SqlCommand(Command, Conn, Tran);
                else
                    Cmd = new SqlCommand(Command, Conn);

                return Cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Message = "BaseDB: " + ex.Message;
                Message = string.Format("BaseDB.RunCommand: {0}. {1}", Command, ex.Message);
            }
            return -1;
        }

        public int BeginTransaction()
        {
            Tran = Conn.BeginTransaction();
            return 0;
        }

        public int CommitTransaction()
        {
            if (Tran == null)
                return 0;
            Tran.Commit();
            Tran.Dispose();
            Tran = null;
            return 0;
        }

        public int RollbackTransaction()
        {
            if (Tran == null)
                return 0;
            Tran.Rollback();
            Tran.Dispose();
            Tran = null;
            return 0;
        }
    }
}

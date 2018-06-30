﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Intersect.Logging;
using Mono.Data.Sqlite;

namespace Intersect.Server.Classes.Core
{
    public class DatabaseConnection
    {
        //Database Variables
        private const string INFO_TABLE = "info";
        private const string DB_VERSION = "dbversion";

        private string mDbFilePath;
        private string mDbFileName;
        private object mDbLock = new object();
        public event EventHandler OnCreateDb;

        public SqliteConnection DbConnection { get; private set; }

        public DatabaseConnection(string databaseFile, EventHandler onCreateDbHandler)
        {
            mDbFilePath = databaseFile;
            mDbFileName = Path.GetFileName(mDbFilePath);
            if (onCreateDbHandler != null) OnCreateDb += onCreateDbHandler;
            if (File.Exists(mDbFilePath))
            {
                Backup();
            }
            else
            {
                Create();
            }

        }

        public void Create()
        {
            DbConnection = new SqliteConnection($"Data Source={mDbFilePath},Version=3,New=True");
            DbConnection?.Open();
            if (OnCreateDb != null) OnCreateDb(this, null);
        }

        public void Open()
        {
            if (DbConnection == null)
            {
                DbConnection = new SqliteConnection("Data Source=" + mDbFilePath + ",Version=3");
                DbConnection.Open();
            }
        }

        public void Backup()
        {
            var backupsToKeep = 360;
            Database.CheckDirectories();
            var sw = new Stopwatch();
            sw.Start();
            lock (mDbLock)
            {
                var connectionOpen = DbConnection != null;
                if (connectionOpen)
                {
                    Close();
                }

                // Get the stream of the source file.
                var fi = new FileInfo(mDbFilePath);
                using (var inFile = fi.OpenRead())
                {
                    // Prevent compressing hidden and already compressed files.
                    if ((File.GetAttributes(fi.FullName) & FileAttributes.Hidden) != FileAttributes.Hidden & fi.Extension != ".gz")
                    {
                        // Create the compressed file.
                        using (var outFile =
                            File.Create($"{Database.DIRECTORY_BACKUPS}/{mDbFileName}_{DateTime.Now:yyyy-MM-dd hh-mm-ss}.db.gz"))
                        {
                            using (var compressionStream =
                                new GZipStream(outFile,
                                    CompressionMode.Compress))
                            {
                                // Copy the source file into the compression stream.
                                inFile.CopyTo(compressionStream);
                            }
                        }
                    }
                }
                Open();
            }
            sw.Stop();
            Log.Info($"Database backup at {DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss")} took  {sw.ElapsedMilliseconds}ms");
            //Delete backups if we have too many!
            var last = Directory.EnumerateFiles("resources/backups")
                .Select(fileName => new FileInfo(fileName))
                .OrderByDescending(fileInfo => fileInfo.LastWriteTime) // or "CreationTime"
                .Skip(backupsToKeep)
                .Select(fileInfo => fileInfo.FullName);
            foreach (var file in last)
            {
                File.Delete(file);
            }
        }

        public SqliteCommand CreateCommand()
        {
            return DbConnection?.CreateCommand();
        }

        public int ExecuteNonQuery(SqliteCommand command)
        {
            lock (mDbLock)
            {
                command.Connection = DbConnection;
                using (var transaction = DbConnection?.BeginTransaction())
                {
                    var returnVal = command.ExecuteNonQuery();
                    transaction.Commit();
                    return returnVal;
                }
            }
        }

        public SqliteDataReader ExecuteReader(SqliteCommand command)
        {
            lock (mDbLock)
            {
                command.Connection = DbConnection;
                return command.ExecuteReader();
            }
        }

        public object ExecuteScalar(SqliteCommand command)
        {
            lock (mDbLock)
            {
                command.Connection = DbConnection;
                return command.ExecuteScalar();
            }
        }

        public SqliteTransaction BeginTransaction()
        {
            return DbConnection.BeginTransaction();
        }

        public long GetVersion()
        {
            if (DbConnection != null && DbConnection.State == ConnectionState.Open)
            {
                var cmd = "SELECT " + DB_VERSION + " from " + INFO_TABLE + ";";
                using (var createCommand = DbConnection.CreateCommand())
                {
                    createCommand.CommandText = cmd;
                    return (long)createCommand.ExecuteScalar();
                }
            }
            return -1;
        }

        public void Close()
        {
            if (DbConnection != null)
            {
                DbConnection.Close();
                DbConnection.Dispose();
                DbConnection = null;
            }
        }
    }
}
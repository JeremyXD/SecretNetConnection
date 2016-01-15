        public void Stop()
        {
            CheckClientUsedAsServer();
            listener.Stop();
            WhenAll(tasks).Wait(); // I Changed this for .NET 4.0
        }
        
        public static Task WhenAll(IEnumerable<Task> tasks)
        {
            return Task.Factory.ContinueWhenAll(tasks.ToArray(), _ => { });
        }
        
        private void ListenAsync()
        {
            Task<TcpClient> acceptTcpClientTask = Task.Factory.FromAsync<TcpClient>(listener.BeginAcceptTcpClient, listener.EndAcceptTcpClient, listener);
            acceptTcpClientTask.ContinueWith(task =>
            {
                NetConnection connection = new NetConnection(task.Result);
                StartReceiveFrom(connection);
                OnConnect(this, connection);
                ListenAsync();
            },
            TaskContinuationOptions.OnlyOnRanToCompletion);
            tasks.Add(acceptTcpClientTask);
        }
        
        private void ReceiveFromAsync(NetConnection client)
        {
            Task receiveTask = Task.Factory.StartNew(() =>
             {
                 try
                 {
                     NetworkStream stream = client.client.GetStream();
                     byte[] buffer = new byte[BufferSize];
                     MemoryStream ms = new MemoryStream();
                     while (client.client.Connected)
                     {
                         Task<int> bytesRead = Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, buffer, 0, buffer.Length, null);
                         bytesRead.ContinueWith(task =>
                         {
                             ms.Write(buffer, 0, task.Result);
                             if (!stream.DataAvailable)
                             {
                                 CallOnDataReceived(client, ms.ToArray());
                                 ms.Seek(0, SeekOrigin.Begin);
                                 ms.SetLength(0);
                             }
                         }, TaskContinuationOptions.OnlyOnRanToCompletion);
                     }
                     CallOnDisconnect(client);
                 }
                 catch (Exception ex)
                 {
                     CallOnDisconnect(client);
                     throw;
                }
             });
            tasks.Add(receiveTask);
        }
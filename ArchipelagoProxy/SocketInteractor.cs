using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ArchipelagoProxy
{
    public class SocketInteractor
    {
        private readonly object _onPacketReceivedLock = new object();
        private event Action<string, string> _onPacketReceived;

        private ManualResetEvent writeEvent = new ManualResetEvent(false);
        private Queue<MessageData> messageQueue = new Queue<MessageData>();

        public void AddPacketReceivedEvent(Action<string, string> evt)
        {
            if (evt != null)
            {
                lock (_onPacketReceivedLock)
                {
                    _onPacketReceived += evt;
                }
            }
            else
            {
                Console.WriteLine("evt is null! Cannot add");
            }
        }

        public void SendPacket(string messageType, string message)
        {
            Console.WriteLine($"Queuing message ({messageType}): {message}");
            lock (writeEvent)
            {
                messageQueue.Enqueue(new MessageData()
                {
                    MessageType = messageType,
                    Message = message
                });
                writeEvent.Set();
            }
        }

        public void InteractUntilConnectionClosed(Socket handler)
        {
            var readEvent = new ManualResetEvent(false);
            var allNetworkEvents = new List<WaitHandle>();
            allNetworkEvents.Add(readEvent);
            allNetworkEvents.Add(writeEvent);
            var allNetworkEventsArr = allNetworkEvents.ToArray();
            Action waitForNetworkEvent = () =>
            {
                var waitedIndex = WaitHandle.WaitAny(allNetworkEventsArr);
                if (waitedIndex == 0) // Write
                {
                    lock (writeEvent)
                    {
                        var messageToSend = messageQueue.Dequeue();
                        var fullValueStr = $"{Constants.MessageTypeStartStr}{messageToSend.MessageType}{Constants.MessageTypeEndStr}{messageToSend.Message}{Constants.MessageEndStr}";
                        handler.Send(Encoding.UTF8.GetBytes(fullValueStr));
                    }
                }
                else if (waitedIndex != 1) // 1 is read (just release execution), anything else is unknown
                {
                    throw new Exception("Unknown WaitHandle index");
                }
            };

            try
            {
                byte[] singleByte = new byte[1];
                StringBuilder messageType = new StringBuilder();
                StringBuilder message = new StringBuilder();
                bool isMessageTypeStarted = false;
                bool isReceivingMessage = false;
                bool continueToInteract = true;
                while (continueToInteract)
                {
                    var beginReceiveResult = handler.BeginReceive(singleByte, 0, 1, SocketFlags.None, (result) =>
                    {
                        if (result.IsCompleted)
                        {
                            if (!isReceivingMessage)
                            {
                                messageType.Append(Encoding.UTF8.GetString(singleByte));
                                if (!isMessageTypeStarted)
                                {
                                    if (singleByte[0] == Constants.MessageTypeStartBytes.Last()
                                        && messageType.Length >= Constants.MessageTypeStartBytes.Length
                                        && messageType.ToString(messageType.Length - Constants.MessageTypeStartBytes.Length,
                                            Constants.MessageTypeStartBytes.Length) == Constants.MessageTypeStartStr)
                                    {
                                        messageType.Remove(messageType.Length - Constants.MessageTypeStartBytes.Length,
                                            Constants.MessageTypeStartBytes.Length);
                                        messageType.Clear(); // Strip out starting indicator, we don't want it
                                        isMessageTypeStarted = true;
                                    }
                                }
                                else
                                {
                                    if (singleByte[0] == Constants.MessageTypeEndBytes.Last()
                                        && messageType.Length >= Constants.MessageTypeEndBytes.Length
                                        && messageType.ToString(messageType.Length - Constants.MessageTypeEndBytes.Length,
                                            Constants.MessageTypeEndBytes.Length) == Constants.MessageTypeEndStr)
                                    {
                                        messageType.Remove(messageType.Length - Constants.MessageTypeEndBytes.Length,
                                            Constants.MessageTypeEndBytes.Length); // Strip out end indicator
                                        // Catch for killing connection -- we don't need to process this elsewhere, we just end the stream
                                        if (messageType.ToString() == Constants.StopConnectionMessageType)
                                        {
                                            continueToInteract = false;
                                        }
                                        isMessageTypeStarted = false;
                                        isReceivingMessage = true;
                                    }
                                }
                            }
                            else
                            {
                                message.Append(Encoding.UTF8.GetString(singleByte));
                                if (singleByte[0] == Constants.MessageEndBytes.Last()
                                    && message.Length >= Constants.MessageEndBytes.Length
                                    && message.ToString(message.Length - Constants.MessageEndBytes.Length, Constants.MessageEndBytes.Length)
                                        == Constants.MessageEndStr)
                                {
                                    // Strip off _messageEnd to produce only the message we care about
                                    message.Remove(message.Length - Constants.MessageEndBytes.Length, Constants.MessageEndBytes.Length);
                                    message.Clear();
                                    isReceivingMessage = false;
                                    lock (_onPacketReceivedLock)
                                    {
                                        _onPacketReceived(messageType.ToString(), message.ToString());
                                    }
                                }
                            }
                            readEvent.Set();
                        }
                    }, new object());
                    // TODO Check if there are any conditions in which we will wait on the mutex after it's been released (this is a deadlock scenario)
                    // This should only occur if there are result states that we didn't account for
                    // If we already read a byte, we will go again, and a future run will eventually have nothing to read (and thus write if necessary)
                    if (beginReceiveResult.CompletedSynchronously || beginReceiveResult.IsCompleted)
                    {
                        waitForNetworkEvent(); // Processes all writes, returns execution if read
                    }
                    // else queue up another read
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        private class MessageData
        {
            public string MessageType { get; set; }
            public string Message { get; set; }
        }
    }
}

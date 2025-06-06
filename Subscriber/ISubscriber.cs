﻿using FastCSharp.Observability;
using Microsoft.Extensions.Configuration;

namespace FastCSharp.Subscriber;

public delegate Task<bool> OnMessageCallback<in T>(T? message);
public delegate T? Handler<T>(T? message);

/// <summary>
/// Serves as independent interface for publishing messages. Usually, implementation uses a
/// Message Queue. In that case, subscriber will connect to the message queue broker and each
/// Subscriber will connect to a specific queue.
/// See the Subscriber implementation for further configuration options.
/// </summary>
/// <typeparam name="T">The type of the message object transmited.</typeparam>
/// <remarks>
/// The Subscriber is a generic class that can be used to subscribe to a message queue or any other
/// message origin. The message origin is defined by the implementation of the ISubscriberFactory
/// interface. The Subscriber is a generic class that can be used to subscribe to a message queue or any other
/// message origin. The message origin is defined by the implementation of the ISubscriberFactory
/// interface.
/// </remarks>
/// <example>
/// 
/// <code>
/// // Create a new SubscriberFactory
/// ISubscriberFactory factory = new SubscriberFactory();
/// 
/// // Create a new Subscriber for the message origin "MyQueue"
/// ISubscriber<MyMessage> subscriber = factory.NewSubscriber<MyMessage>("MyQueue");
/// 
/// // Register a callback function to process the message
/// subscriber.Register((MyMessage? message) => {
///    // Do something with the message
///   return true;
/// });
/// 
/// // Start the subscriber
/// subscriber.Start();
/// </code>
/// 
/// </example>
public interface ISubscriber<T> : IDisposable, IHealthReporter
{
    /// <summary>
    /// Registers the callback function as a message listenner.
    /// Returns self to chain the addition of message handlers
    /// </summary>
    /// <param name="callback">the function to register as message listenner.</param>
    /// <returns></returns>
    public Task<ISubscriber<T>> RegisterAsync(OnMessageCallback<T> callback);

    /// <summary>
    /// Registers handler function to handle message before deelivering to the callback
    /// Returns self to chain the addition of message handlers
    /// </summary>
    /// <param name="handler"></param>
    /// <returns></returns>
    public ISubscriber<T> AddMsgHandler(Handler<T> handler);

    /// <summary>
    /// Resets the subscriber to its initial state. This will register a new consumer and, 
    /// if needed, a new connection and channel.
    /// 
    /// You may use this method to reset the subscriber after unsubscribing. For example, if you
    /// want to stop the subscriber for a while and then start it again, you should call this
    /// method to reset the subscriber.
    /// 
    /// This method is useful when the connection to the message broker is lost and the
    /// subscriber is not able to recover the connection.
    /// </summary>
    public Task ResetAsync();

    /// <summary>
    /// Unsubscribes the consumer from the message queue. This will stop the subscriber from
    /// receiving messages.
    /// 
    /// You may use this method to stop the subscriber for a while and then start it again using 
    /// the Reset method. This is useful to implement a pause/resume functionality such as a
    /// backoff mechanism.
    /// </summary>
    public Task UnSubscribeAsync();

    /// <summary>
    /// Returns true if the subscriber is healthy and able to receive messages.
    /// </summary>
    public bool IsHealthy { get; }

    public IConfigurationSection? Options { get;}
}

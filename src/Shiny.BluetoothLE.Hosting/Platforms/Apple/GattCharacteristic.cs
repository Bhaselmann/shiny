﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreBluetooth;
using Foundation;

namespace Shiny.BluetoothLE.Hosting;


public class GattCharacteristic : IGattCharacteristic, IGattCharacteristicBuilder, IDisposable
{
    readonly CBPeripheralManager manager;
    readonly PeripheralCache cache;

    CBMutableCharacteristic native = null!;
    Func<CharacteristicSubscription, Task>? onSubscribe;
    Func<WriteRequest, Task<GattState>>? onWrite;
    Func<ReadRequest, Task<ReadResult>>? onRead;

    CBAttributePermissions permissions = 0;
    CBCharacteristicProperties properties = 0;


    public GattCharacteristic(CBPeripheralManager manager, string uuid)
    {
        this.cache = new PeripheralCache();
        this.manager = manager;
        this.Uuid = uuid;
    }


    public string Uuid { get; }
    public CharacteristicProperties Properties { get; }
    public IReadOnlyList<IPeripheral> SubscribedCentrals => this.cache.Subscribed;


    public async Task Notify(byte[] data, params IPeripheral[] centrals)
    {
        var success = this.manager.UpdateValue(
            NSData.FromArray(data),
            this.native,
            null
        );
        if (!success)
        {
            var tcs = new TaskCompletionSource<bool>();
            var handler = new EventHandler((sender, args) =>
            {
                this.manager.UpdateValue(
                    NSData.FromArray(data),
                    this.native,
                    null
                );
                tcs.TrySetResult(true);
            });

            try
            {
                this.manager.ReadyToUpdateSubscribers += handler;
                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                this.manager.ReadyToUpdateSubscribers -= handler;
            }
        }
    }


    public IGattCharacteristicBuilder SetNotification(Func<CharacteristicSubscription, Task>? onSubscribe = null, NotificationOptions options = NotificationOptions.Notify)
    {
        this.onSubscribe = onSubscribe;
        var enc = options.HasFlag(NotificationOptions.EncryptionRequired);

        if (options.HasFlag(NotificationOptions.Indicate))
            this.properties |= enc
                ? CBCharacteristicProperties.IndicateEncryptionRequired
                : CBCharacteristicProperties.Indicate;

        if (options.HasFlag(NotificationOptions.Notify))
            this.properties |= enc
                ? CBCharacteristicProperties.NotifyEncryptionRequired
                : CBCharacteristicProperties.Notify;

        return this;
    }


    public IGattCharacteristicBuilder SetRead(Func<ReadRequest, Task<ReadResult>> onRead, bool encrypted = false)
    {
        this.onRead = onRead;
        this.permissions |= encrypted
            ? CBAttributePermissions.ReadEncryptionRequired
            : CBAttributePermissions.Readable;

        this.properties |= CBCharacteristicProperties.Read;

        return this;
    }


    public IGattCharacteristicBuilder SetWrite(Func<WriteRequest, Task<GattState>> onWrite, WriteOptions options = WriteOptions.Write)
    {
        this.onWrite = onWrite;
        this.permissions |= options.HasFlag(WriteOptions.EncryptionRequired)
            ? CBAttributePermissions.WriteEncryptionRequired
            : CBAttributePermissions.Writeable;

        if (options.HasFlag(WriteOptions.Write))
            this.properties |= CBCharacteristicProperties.Write;

        if (options.HasFlag(WriteOptions.WriteWithoutResponse))
            this.properties |= CBCharacteristicProperties.WriteWithoutResponse;

        if (options.HasFlag(WriteOptions.AuthenticatedSignedWrites))
            this.properties |= CBCharacteristicProperties.AuthenticatedSignedWrites;

        return this;
    }


    public void Build(CBMutableService service)
    {
        if (this.onWrite != null)
            this.manager.WriteRequestsReceived += this.OnWrite!;

        if (this.onRead != null)
            this.manager.ReadRequestReceived += this.OnRead!;

        if (this.onSubscribe != null)
        {
            this.manager.CharacteristicSubscribed += this.OnSubscribed!;
            this.manager.CharacteristicUnsubscribed += this.OnUnSubscribed!;
        }

        this.native = new CBMutableCharacteristic(
            CBUUID.FromString(this.Uuid),
            this.properties,
            null,
            this.permissions
        );
        service.Characteristics = service.Characteristics!.Expand(this.native);
    }


    public void Dispose()
    {
        this.manager.WriteRequestsReceived -= this.OnWrite!;
        this.manager.ReadRequestReceived -= this.OnRead!;
        this.manager.CharacteristicSubscribed -= this.OnSubscribed!;
        this.manager.CharacteristicUnsubscribed -= this.OnUnSubscribed!;
    }


    bool IsThis(CBCharacteristic arg) => this.native.Equals(arg);

    async void OnSubChanged(CBPeripheralManagerSubscriptionEventArgs args, bool subscribed)
    {
        if (!this.IsThis(args.Characteristic))
            return;

        var peripheral = this.cache.SetSubscription(args.Central, subscribed);
        var sub = new CharacteristicSubscription(this, peripheral, subscribed);
        await this.onSubscribe!.Invoke(sub);
    }


    void OnSubscribed(object sender, CBPeripheralManagerSubscriptionEventArgs args) => this.OnSubChanged(args, true);
    void OnUnSubscribed(object sender, CBPeripheralManagerSubscriptionEventArgs args) => this.OnSubChanged(args, false);


    async void OnWrite(object sender, CBATTRequestsEventArgs args)
    {
        foreach (var req in args.Requests)
        {
            if (this.IsThis(req.Characteristic))
            {
                var peripheral = this.cache.GetOrAdd(req.Central);
                var result = await this.onWrite!.Invoke(new WriteRequest(
                    this,
                    peripheral,
                    req.Value?.ToArray(),
                    (int) req.Offset,
                    true
                ));
                this.manager.RespondToRequest(req, CBATTError.Success);
            }
        }
    }


    async void OnRead(object sender, CBATTRequestEventArgs args)
    {
        if (!args.Request.Characteristic.Equals(this.native))
            return;

        var peripheral = this.cache.GetOrAdd(args.Request.Central);
        var result = await this.onRead!.Invoke(new ReadRequest(
            this,
            peripheral,
            (int)args.Request.Offset)
        );
        if (result.Status == GattState.Success)
        {
            args.Request.Value = NSData.FromArray(result.Data!);
            this.manager.RespondToRequest(args.Request, CBATTError.Success);
        }
        else
        {
            this.manager.RespondToRequest(args.Request, CBATTError.InsufficientEncryption);
        }
    }
}

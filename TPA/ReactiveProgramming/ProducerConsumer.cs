﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using TPA.AsynchronousBehavior.ReactiveProgramming;

namespace ReactiveProgrammingUnitTest
{
    public class Producer<TProduct> : ISubject<bool, TProduct>
    {
        public Producer(Func<TProduct> produce, TimeSpan producePeriod, int bufferCapacity)
        {
            Produce = produce;
            ProducePeriod = producePeriod;
            BufferCapacity = bufferCapacity;
            _produceSubscriber = Observable
                .Interval(ProducePeriod)
                .Synchronize(_bufferLockObject)
                .Subscribe(_ => ProduceToBuffer());
        }

        public void OnCompleted()
        {
            _produceSubscriber.Dispose();
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(bool value)
        {
            lock (_bufferLockObject)
            {
                EmitToConsumer();
            }
        }

        public IDisposable Subscribe(IObserver<TProduct> observer)
        {
            ConsumerObserver = observer;
            return Disposable.Create(() => ConsumerObserver = null);
        }

        private void ProduceToBuffer()
        {
            TProduct product = Produce();

            if (_buffer.Count == BufferCapacity)
                _buffer.Dequeue();

            _buffer.Enqueue(product);

            if (_productRequested)
            {
                EmitToConsumer();
                _productRequested = false;
            }
        }

        private void EmitToConsumer()
        {
            if (ConsumerObserver == null)
                return;

            if (_buffer.Count > 0)
            {
                TProduct product = _buffer.Dequeue();
                ConsumerObserver?.OnNext(product);
            }
            else
                _productRequested = true;
        }

        public Func<TProduct> Produce
        {
            get;
        }

        public TimeSpan ProducePeriod
        {
            get;
        }

        public int BufferCapacity
        {
            get;
        }

        public IObserver<TProduct> ConsumerObserver
        {
            get;
            private set;
        }

        private IDisposable _produceSubscriber;
        private Queue<TProduct> _buffer = new Queue<TProduct>();
        private object _bufferLockObject = new object();
        private bool _productRequested = false;
    }

    public class Consumer<TProduct> : ISubject<TProduct, bool>
    {
        public Consumer(Action<TProduct> consume, TimeSpan consumePeriod)
        {
            Consume = consume;
            ConsumePeriod = consumePeriod;
        }

        public void OnCompleted()
        {
            _productRequestSubscriber?.Dispose();
            ProducerObserver?.OnCompleted();
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(TProduct value)
        {
            ProcessProduct(value);
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            ProducerObserver = observer;
            NotifyProducer();
            return Disposable.Create(() => ProducerObserver = null);
        }

        private void ProcessProduct(TProduct product)
        {
            _productRequestSubscriber?.Dispose();

            if (ProducerObserver == null)
                return;

            Consume(product);
            _productRequestSubscriber = Observable
                .Timer(ConsumePeriod)
                .Subscribe(_ => NotifyProducer());
        }

        private void NotifyProducer()
        {
            ProducerObserver?.OnNext(true);
        }

        public Action<TProduct> Consume
        {
            get;
        }

        public TimeSpan ConsumePeriod
        {
            get;
        }

        public IObserver<bool> ProducerObserver
        {
            get;
            private set;
        }

        private IDisposable _productRequestSubscriber;
    }
}

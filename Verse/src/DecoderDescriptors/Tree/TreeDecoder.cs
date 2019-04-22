﻿using System;
using System.IO;

namespace Verse.DecoderDescriptors.Tree
{
    internal class TreeDecoder<TState, TNative, TEntity> : IDecoder<TEntity>
    {
        public event DecodeError Error;

        private readonly ReaderCallback<TState, TNative, TEntity> callback;

        private readonly Func<TEntity> constructor;

        private readonly IReaderSession<TState, TNative> session;

        public TreeDecoder(IReaderSession<TState, TNative> session, Func<TEntity> constructor, ReaderCallback<TState, TNative, TEntity> callback)
        {
            this.callback = callback;
            this.constructor = constructor;
            this.session = session;
        }

        public bool TryOpen(Stream input, out IDecoderStream<TEntity> decoderStream)
        {
            if (!this.session.Start(input, (p, m) => this.Error?.Invoke(p, m), out var state))
            {
                decoderStream = default;

                return false;
            }

            decoderStream =
                new TreeDecoderStream<TState, TNative, TEntity>(this.session, this.constructor, this.callback, state);

            return true;
        }
    }
}

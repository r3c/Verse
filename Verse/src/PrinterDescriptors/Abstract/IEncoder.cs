using System;

namespace Verse.PrinterDescriptors.Abstract
{
    internal interface IEncoder<TOutput>
    {
        #region Methods

        Converter<TInput, TOutput> Get<TInput>();

        #endregion
    }
}
﻿namespace Translatable.SampleProject
{
    [Translatable]
    public enum TestEnum
    {
        [TranslatorComment("Test")]
        [Translation("This is the first value")]
        FirstValue,
        [Translation("This is the second value")]
        SecondValue,
        [Translation("This is some extraordinary third value!")]
        ThirdValue
    }
}

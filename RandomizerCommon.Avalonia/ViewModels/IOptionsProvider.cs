namespace RandomizerCommon;

// Methods for ViewModels to return current options. Seeds need not be set.
public interface IOptionsProvider
{
    public RandomizerOptions MakeOptions();
    public RandomizerOptions MakeDefaultOptions();
}
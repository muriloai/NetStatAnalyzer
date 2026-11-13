using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetStatAnalyzer.Presentation.Common
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private string _tagText = string.Empty;
        public string TagText
        {
            get => _tagText;
            set => SetProperty(ref _tagText, value);
        }

        private bool _annotationTrusted;
        public bool AnnotationTrusted
        {
            get => _annotationTrusted;
            set
            {
                if (SetProperty(ref _annotationTrusted, value)) OnPropertyChanged(nameof(AnnotationTrustText));
            }
        }

        public string AnnotationTrustText => AnnotationTrusted ? "Confiável" : "Não verificado";
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}

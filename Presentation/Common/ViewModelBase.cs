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
            set
            {
                if (SetProperty(ref _tagText, value))
                {
                    OnPropertyChanged(nameof(TagsList));
                    OnPropertyChanged(nameof(HasTags));
                }
            }
        }

        public IReadOnlyList<string> TagsList => string.IsNullOrWhiteSpace(TagText)
            ? System.Array.Empty<string>()
            : TagText.Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                     .Where(t => !string.IsNullOrWhiteSpace(t))
                     .Distinct(System.StringComparer.OrdinalIgnoreCase)
                     .ToArray();

        public bool HasTags => TagsList.Count > 0;

        private bool _annotationTrusted;
        public bool AnnotationTrusted
        {
            get => _annotationTrusted;
            set
            {
                if (SetProperty(ref _annotationTrusted, value))
                {
                    OnPropertyChanged(nameof(AnnotationTrustText));
                    OnPropertyChanged(nameof(AnnotationTrustToolTip));
                }
            }
        }

        private bool _hasHashMismatch;
        public bool HasHashMismatch
        {
            get => _hasHashMismatch;
            set
            {
                if (SetProperty(ref _hasHashMismatch, value))
                {
                    OnPropertyChanged(nameof(AnnotationTrustText));
                    OnPropertyChanged(nameof(AnnotationTrustToolTip));
                }
            }
        }

        public string AnnotationTrustText => AnnotationTrusted
            ? "Confiável"
            : (HasHashMismatch ? "⚠️ Hash alterado" : "Não verificado");

        public string AnnotationTrustToolTip => AnnotationTrusted
            ? "Item verificado e marcado como confiável."
            : (HasHashMismatch
                ? "⚠️ O executável foi modificado ou atualizado em disco desde a aprovação da confiança. Revalide a confiança do item para aprovar a nova versão."
                : "Sem anotação de confiança cadastrada.");
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

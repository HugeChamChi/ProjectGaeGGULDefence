using System;

namespace HSD.UI.Upgrade
{
    public class UpgradePresenter : IDisposable
    {
        private readonly UpgradeModel _model;
        private readonly UI_UpgradePanel _view;

        public UpgradePresenter(UpgradeModel model, UI_UpgradePanel view)
        {
            _model = model;
            _view = view;

            _view.OnOpened += HandleOpened;
            _model.OnDataChanged += UpdateView;
            _model.OnCurrencyChanged += HandleCurrencyChanged;
        }

        private void HandleOpened()
        {
            _view.InitItems(_model.GetUpgradeItems(), HandleUpgradeClicked);
            _view.SetCurrency(_model.GetCurrentCurrency());
        }

        private void HandleUpgradeClicked(string target)
        {
            if (_model.TryUpgrade(target))
            {
                // UI에서 즉각적인 피드백이 필요하다면 여기서 처리 (ex: 효과음)
            }
        }

        private void UpdateView()
        {
            _view.UpdateAllItems(_model.GetUpgradeItems());
        }

        private void HandleCurrencyChanged(float amount)
        {
            _view.SetCurrency(amount);
        }

        public void Dispose()
        {
            _view.OnOpened -= HandleOpened;
            _model.OnDataChanged -= UpdateView;
            _model.OnCurrencyChanged -= HandleCurrencyChanged;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace QuoteHistoryGUI
{
    public class SingleDelegateCommand : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public delegate bool ExecuteDelegate(object param, bool isCheckOnly);

        private readonly ExecuteDelegate _execDelegate;

        public SingleDelegateCommand(ExecuteDelegate executeDelagate)
        {
            _execDelegate = executeDelagate;
        }

        #region Implementation of ICommand

        public void Execute(object parameter)
        {
            if (_execDelegate != null)
                _execDelegate(parameter, false);
        }

        public bool CanExecute(object parameter)
        {
            if (_execDelegate != null)
                return _execDelegate(parameter, true);
            return false;
        }

        #endregion
    }
}

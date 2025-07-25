/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NLog;

namespace Chummer.Controls.Shared
{
    public partial class ObservableCollectionDisplay<TType> : UserControl
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(ObservableCollectionDisplay<TType>));
        public PropertyChangedEventHandler ChildPropertyChanged { get; set; }
        private readonly ConcurrentHashSet<PropertyChangedAsyncEventHandler> _setChildPropertyChangedAsync =
            new ConcurrentHashSet<PropertyChangedAsyncEventHandler>();

        public event PropertyChangedAsyncEventHandler ChildPropertyChangedAsync
        {
            add => _setChildPropertyChangedAsync.TryAdd(value);
            remove => _setChildPropertyChangedAsync.Remove(value);
        }

        private int _intSuspendLayoutCount;
        private readonly Func<TType, Control> _funcCreateControl;  //Function to create a control out of a item
        private readonly bool _blnLoadVisibleOnly;
        private readonly List<ControlWithMetaData> _lstContentList = new List<ControlWithMetaData>(10);
        private readonly List<int> _lstDisplayIndex = new List<int>(10);
        private readonly IndexComparer _indexComparer;
        private bool[] _ablnRendered; // Not BitArray because read/write performance is much more important here than memory footprint
        private int _intOffScreenChunkSize = 1;
        private int _intListItemControlHeight;
        private bool _blnAllRendered;
        private Predicate<TType> _visibleFilter = x => true;
        private Func<TType, CancellationToken, Task<bool>> _visibleFilterAsync = DefaultVisibleAsync;
        private IComparer<TType> _comparison;
        private Func<TType, TType, CancellationToken, Task<int>> _comparisonAsync;

        public ObservableCollectionDisplay(ThreadSafeObservableCollection<TType> contents, Func<TType, Control> funcCreateControl, bool blnLoadVisibleOnly = true)
        {
            InitializeComponent();
            Disposed += (sender, args) =>
            {
                foreach (ControlWithMetaData _objControlWithMetaData in _lstContentList)
                {
                    _objControlWithMetaData.Dispose();
                }
            };
            Contents = contents ?? throw new ArgumentNullException(nameof(contents));
            _funcCreateControl = funcCreateControl;
            _blnLoadVisibleOnly = blnLoadVisibleOnly;
            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                pnlDisplay.SuspendLayout();
            try
            {
                int intMaxControlHeight = 0;
                foreach (TType objLoopTType in Contents.AsEnumerableWithSideEffects())
                {
                    ControlWithMetaData objNewControl = new ControlWithMetaData(objLoopTType, this, false);
                    intMaxControlHeight = Math.Max(objNewControl.Control.PreferredSize.Height, intMaxControlHeight);
                    _lstContentList.Add(objNewControl);
                }

                if (intMaxControlHeight > 0)
                    ListItemControlHeight = intMaxControlHeight;

                pnlDisplay.Controls.AddRange(_lstContentList.Select(x => x.Control).ToArray());
                _indexComparer = new IndexComparer(Contents);
                _comparison = _comparison ?? _indexComparer;
                _comparisonAsync = _comparisonAsync ?? ((x, y, z) => DefaultCompareAsync(_indexComparer, x, y, z));

                Contents.CollectionChangedAsync += OnCollectionChanged;
                Disposed += (sender, args) =>
                {
                    try
                    {
                        Contents.CollectionChangedAsync -= OnCollectionChanged;
                    }
                    catch (ObjectDisposedException)
                    {
                        //swallow this
                    }
                };
                ComputeDisplayIndex();
                LoadScreenContent();
                ObservableCollectionDisplay_SizeChanged(null, null);
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    pnlDisplay.ResumeLayout();
            }
        }

        private void ObservableCollectionDisplay_Load(object sender, EventArgs e)
        {
            Application.Idle += ApplicationOnIdle;
            Disposed += (o, args) => Application.Idle -= ApplicationOnIdle;
        }

        /// <summary>
        /// Base ObservableCollection that represents all possible contents of the display, not necessarily all visible.
        /// </summary>
        public ThreadSafeObservableCollection<TType> Contents { get; }

        public Panel DisplayPanel => pnlDisplay;

        private int ListItemControlHeight
        {
            get => _intListItemControlHeight;
            set
            {
                if (Interlocked.Exchange(ref _intListItemControlHeight, value) == value)
                    return;
                foreach (ControlWithMetaData objControl in _lstContentList)
                {
                    objControl.UpdateHeight();
                }
            }
        }

        private async Task SetListItemControlHeightAsync(int value, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (Interlocked.Exchange(ref _intListItemControlHeight, value) == value)
                return;
            foreach (ControlWithMetaData objControl in _lstContentList)
            {
                await objControl.UpdateHeightAsync(token).ConfigureAwait(false);
            }
        }

        private void LoadRange(int min, int max)
        {
            min = Math.Max(0, min);
            max = Math.Min(_lstDisplayIndex.Count, max);
            if (_ablnRendered.FirstMatching(false, min) > max)
                return;
            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                pnlDisplay.DoThreadSafe(x => x.SuspendLayout());
            try
            {
                for (int i = min; i < max; ++i)
                {
                    if (_ablnRendered[i])
                        continue;

                    ControlWithMetaData item = _lstContentList[_lstDisplayIndex[i]];
                    int intLocal = i;
                    item.Control.DoThreadSafe(x =>
                    {
                        x.Location = new Point(0, intLocal * ListItemControlHeight);
                        x.Visible = true;
                    });
                    _ablnRendered[i] = true;
                }
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    pnlDisplay.DoThreadSafe(x => x.ResumeLayout());
            }
        }

        private async Task LoadRangeAsync(int min, int max, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            min = Math.Max(0, min);
            max = Math.Min(_lstDisplayIndex.Count, max);
            if (_ablnRendered.FirstMatching(false, min) > max)
                return;
            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                await pnlDisplay.DoThreadSafeAsync(x => x.SuspendLayout(), token: token).ConfigureAwait(false);
            try
            {
                for (int i = min; i < max; ++i)
                {
                    if (_ablnRendered[i])
                        continue;

                    ControlWithMetaData item = _lstContentList[_lstDisplayIndex[i]];
                    int intLocal = i;
                    await (await item.GetControlAsync(token).ConfigureAwait(false)).DoThreadSafeAsync(x =>
                    {
                        x.Location = new Point(0, intLocal * ListItemControlHeight);
                        x.Visible = true;
                    }, token: token).ConfigureAwait(false);
                    _ablnRendered[i] = true;
                }
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    await pnlDisplay.DoThreadSafeAsync(x => x.ResumeLayout(), token: token).ConfigureAwait(false);
            }
        }

        private void ComputeDisplayIndex()
        {
            List<Tuple<TType, int>> objTTypeList = new List<Tuple<TType, int>>(_lstContentList.Count);
            for (int i = 0; i < _lstContentList.Count; ++i)
            {
                ControlWithMetaData objLoopControl = _lstContentList[i];
                if (objLoopControl.Visible)
                {
                    objTTypeList.Add(new Tuple<TType, int>(objLoopControl.Item, i));
                }
            }

            objTTypeList.Sort((x, y) => _comparison.Compare(x.Item1, y.Item1));
            int intDisplayIndexCount = _lstDisplayIndex.Count;

            // Array is temporary and of primitives, so stackalloc used instead of List.ToArray() (which would put the array on the heap) when possible
            int[] aintSharedOldDisplayIndexes = intDisplayIndexCount > GlobalSettings.MaxStackLimit
                ? ArrayPool<int>.Shared.Rent(intDisplayIndexCount)
                : null;
            try
            {
                // ReSharper disable once MergeConditionalExpression
#pragma warning disable IDE0029 // Use coalesce expression
                Span<int> aintOldDisplayIndex = aintSharedOldDisplayIndexes != null
                    ? aintSharedOldDisplayIndexes
                    : stackalloc int[intDisplayIndexCount];
#pragma warning restore IDE0029 // Use coalesce expression
                for (int i = 0; i < intDisplayIndexCount; ++i)
                    aintOldDisplayIndex[i] = _lstDisplayIndex[i];
                _lstDisplayIndex.Clear();
                _lstDisplayIndex.AddRange(objTTypeList.Select(x => x.Item2));
                intDisplayIndexCount = _lstDisplayIndex.Count;

                if (_ablnRendered == null || _ablnRendered.Length != intDisplayIndexCount)
                    _ablnRendered = new bool[_lstDisplayIndex.Count];
                else
                {
                    for (int i = 0; i < _ablnRendered.Length; ++i)
                    {
                        _ablnRendered[i] &= _lstDisplayIndex[i] == aintOldDisplayIndex[i];
                    }
                }
            }
            finally
            {
                if (aintSharedOldDisplayIndexes != null)
                    ArrayPool<int>.Shared.Return(aintSharedOldDisplayIndexes);
            }
        }

        private async Task ComputeDisplayIndexAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<Tuple<TType, int>> objTTypeList = new List<Tuple<TType, int>>(_lstContentList.Count);
            for (int i = 0; i < _lstContentList.Count; ++i)
            {
                ControlWithMetaData objLoopControl = _lstContentList[i];
                if (objLoopControl.Visible)
                {
                    objTTypeList.Add(new Tuple<TType, int>(objLoopControl.Item, i));
                }
            }

            await objTTypeList.SortAsync((x, y) => _comparisonAsync(x.Item1, y.Item1, token), token: token).ConfigureAwait(false);

            // Can't use stackalloc in async methods, so always use array pool instead
            using (new FetchSafelyFromArrayPool<int>(ArrayPool<int>.Shared, _lstDisplayIndex.Count, out int[] aintOldDisplayIndex))
            {
                for (int i = 0; i < _lstDisplayIndex.Count; ++i)
                    aintOldDisplayIndex[i] = _lstDisplayIndex[i];
                _lstDisplayIndex.Clear();
                _lstDisplayIndex.AddRange(objTTypeList.Select(x => x.Item2));

                if (_ablnRendered == null || _ablnRendered.Length != _lstDisplayIndex.Count)
                    _ablnRendered = new bool[_lstDisplayIndex.Count];
                else
                {
                    for (int i = 0; i < _ablnRendered.Length; ++i)
                    {
                        _ablnRendered[i] &= _lstDisplayIndex[i] == aintOldDisplayIndex[i];
                    }
                }
            }
        }

        private void LoadScreenContent()
        {
            if (_lstContentList.Count == 0 || ListItemControlHeight == 0)
                return;

            int toload = _blnLoadVisibleOnly
                ? NumVisibleElements
                : _lstContentList.Count;

            int top = VerticalScroll.Value / ListItemControlHeight;

            LoadRange(top, top + toload);
        }

        private async Task LoadScreenContentAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (_lstContentList.Count == 0 || ListItemControlHeight == 0)
                return;

            int toload = _blnLoadVisibleOnly
                ? await GetNumVisibleElementsAsync(token).ConfigureAwait(false)
                : _lstContentList.Count;

            int top = VerticalScroll.Value / ListItemControlHeight;

            await LoadRangeAsync(top, top + toload, token).ConfigureAwait(false);
        }

        private int NumVisibleElements
        {
            get
            {
                if (_lstContentList.Count == 0 || ListItemControlHeight == 0)
                    return 0;
                return Math.Min(this.DoThreadSafeFunc(x => x.Height) / ListItemControlHeight + 2, _lstContentList.Count);
            }
        }

        private async Task<int> GetNumVisibleElementsAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (_lstContentList.Count == 0 || ListItemControlHeight == 0)
                return 0;
            return Math.Min(await this.DoThreadSafeFuncAsync(x => x.Height, token).ConfigureAwait(false) / ListItemControlHeight + 2, _lstContentList.Count);
        }

        private void ResetDisplayPanelHeight(int intNumVisible = -1)
        {
            int intMyHeight = this.DoThreadSafeFunc(x => x.Height);
            pnlDisplay.DoThreadSafe(x => x.Height = Math.Max(intMyHeight,
                (intNumVisible >= 0 ? intNumVisible : _lstContentList.Count(y => y.Visible)) * ListItemControlHeight));
        }

        private async Task ResetDisplayPanelHeightAsync(int intNumVisible = -1, CancellationToken token = default)
        {
            int intMyHeight = await this.DoThreadSafeFuncAsync(x => x.Height, token: token).ConfigureAwait(false);
            await pnlDisplay.DoThreadSafeAsync(x => x.Height = Math.Max(intMyHeight,
                    (intNumVisible >= 0 ? intNumVisible : _lstContentList.Count(y => y.Visible)) *
                    ListItemControlHeight),
                token: token).ConfigureAwait(false);
        }

        private void RedrawControls(IEnumerable<ControlWithMetaData> lstToClear)
        {
            _blnAllRendered = false;
            int intNumVisible = _lstContentList.Count(x => x.Visible);
            foreach (ControlWithMetaData item in lstToClear)
            {
                if (item.Visible)
                    --intNumVisible;
                item.RefreshVisible();
                if (item.Visible)
                    ++intNumVisible;
            }
            ResetDisplayPanelHeight(intNumVisible);
            ComputeDisplayIndex();
            LoadScreenContent();
        }

        private async Task RedrawControlsAsync(IEnumerable<ControlWithMetaData> lstToClear, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            _blnAllRendered = false;
            int intNumVisible = _lstContentList.Count(x => x.Visible);
            foreach (ControlWithMetaData item in lstToClear)
            {
                if (item.Visible)
                    --intNumVisible;
                await item.RefreshVisibleAsync(token).ConfigureAwait(false);
                if (item.Visible)
                    ++intNumVisible;
            }
            await ResetDisplayPanelHeightAsync(intNumVisible, token).ConfigureAwait(false);
            await ComputeDisplayIndexAsync(token).ConfigureAwait(false);
            await LoadScreenContentAsync(token).ConfigureAwait(false);
        }

        private void ApplicationOnIdle(object sender, EventArgs eventArgs)
        {
            if (_blnAllRendered)
                return;
            int firstUnrendered = _ablnRendered.FirstMatching(false);
            if (firstUnrendered == -1)
            {
                _blnAllRendered = true;
                return;
            }

            int end = _ablnRendered.FirstMatching(true, firstUnrendered);
            if (end == -1)
                end = _lstDisplayIndex.Count;

            end = Math.Min(end, firstUnrendered + _intOffScreenChunkSize);
            using (new FetchSafelyFromSafeObjectPool<Stopwatch>(Utils.StopwatchPool, out Stopwatch sw))
            {
                sw.Start();

                if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                    pnlDisplay.SuspendLayout();
                try
                {
                    LoadRange(firstUnrendered, end);
                }
                finally
                {
                    if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                        pnlDisplay.ResumeLayout();
                }

                sw.Stop();

                if (sw.Elapsed > TimeSpan.FromSeconds(0.1f))
                {
                    if (_intOffScreenChunkSize > 1)
                    {
                        _intOffScreenChunkSize /= 2;
                        Log.Trace("Offscreen chunk render size decreased to " +
                                  _intOffScreenChunkSize.ToString(GlobalSettings.InvariantCultureInfo));
                    }
                }
                else if (sw.Elapsed < TimeSpan.FromSeconds(0.05f))
                {
                    _intOffScreenChunkSize *= 2;
                    Log.Trace("Offscreen chunk render size increased to " +
                              _intOffScreenChunkSize.ToString(GlobalSettings.InvariantCultureInfo));
                }
            }
        }

        public void Filter(Predicate<TType> predicate, Func<TType, CancellationToken, Task<bool>> predicateAsync = null, bool forceRefresh = false)
        {
            if (ReferenceEquals(Interlocked.Exchange(ref _visibleFilter, predicate), predicate))
            {
                if (!forceRefresh)
                    return;
            }
            else
            {
                _visibleFilterAsync = predicateAsync ?? ((x, y) => DefaultVisibleAsync(predicate, x, y));
            }

            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                pnlDisplay.DoThreadSafe(x => x.SuspendLayout());
            try
            {
                RedrawControls(_lstContentList);
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    pnlDisplay.DoThreadSafe(x => x.ResumeLayout());
            }
        }

        public async Task FilterAsync(Predicate<TType> predicate, Func<TType, CancellationToken, Task<bool>> predicateAsync = null, bool forceRefresh = false, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (ReferenceEquals(Interlocked.Exchange(ref _visibleFilter, predicate), predicate))
            {
                if (!forceRefresh)
                    return;
            }
            else
            {
                _visibleFilterAsync = predicateAsync ?? ((x, y) => DefaultVisibleAsync(predicate, x, y));
            }

            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                await pnlDisplay.DoThreadSafeAsync(x => x.SuspendLayout(), token: token).ConfigureAwait(false);
            try
            {
                await RedrawControlsAsync(_lstContentList, token).ConfigureAwait(false);
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    await pnlDisplay.DoThreadSafeAsync(x => x.ResumeLayout(), token: token).ConfigureAwait(false);
            }
        }

        public void Sort(IComparer<TType> comparison, Func<TType, TType, CancellationToken, Task<int>> comparisonAsync = null)
        {
            if (ReferenceEquals(Interlocked.Exchange(ref _comparison, comparison), comparison))
                return;

            _comparisonAsync = comparisonAsync ?? ((x, y, z) => DefaultCompareAsync(comparison, x, y, z));

            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                pnlDisplay.DoThreadSafe(x => x.SuspendLayout());
            try
            {
                RedrawControls(_lstContentList);
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    pnlDisplay.DoThreadSafe(x => x.ResumeLayout());
            }
        }

        public async Task SortAsync(IComparer<TType> comparison, Func<TType, TType, CancellationToken, Task<int>> comparisonAsync = null, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (ReferenceEquals(Interlocked.Exchange(ref _comparison, comparison), comparison))
                return;

            _comparisonAsync = comparisonAsync ?? ((x, y, z) => DefaultCompareAsync(comparison, x, y, z));

            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                await pnlDisplay.DoThreadSafeAsync(x => x.SuspendLayout(), token: token).ConfigureAwait(false);
            try
            {
                await RedrawControlsAsync(_lstContentList, token).ConfigureAwait(false);
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    await pnlDisplay.DoThreadSafeAsync(x => x.ResumeLayout(), token: token).ConfigureAwait(false);
            }
        }

        private async Task OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            IEnumerable<ControlWithMetaData> lstToRedraw = null;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        int intIndex = e.NewStartingIndex;
                        foreach (TType objNewItem in e.NewItems)
                            _lstContentList.Insert(intIndex++, await ControlWithMetaData.GetNewAsync(objNewItem, this, token: token).ConfigureAwait(false));
                        _indexComparer.Reset(Contents);
                        lstToRedraw = _lstContentList.Skip(e.NewStartingIndex);
                        break;
                    }
                case NotifyCollectionChangedAction.Remove:
                    {
                        int intIndex = e.OldStartingIndex;
                        foreach (TType _ in e.OldItems)
                        {
                            await _lstContentList[intIndex].CleanupAsync(token).ConfigureAwait(false);
                            _lstContentList.RemoveAt(intIndex);
                        }
                        _indexComparer.Reset(Contents);
                        lstToRedraw = _lstContentList.Skip(e.OldStartingIndex);
                        break;
                    }
                case NotifyCollectionChangedAction.Replace:
                    {
                        int intIndex = e.OldStartingIndex;
                        foreach (TType _ in e.OldItems)
                        {
                            await _lstContentList[intIndex].CleanupAsync(token).ConfigureAwait(false);
                            _lstContentList.RemoveAt(intIndex);
                        }
                        foreach (TType objNewItem in e.NewItems)
                            _lstContentList.Insert(intIndex++, await ControlWithMetaData.GetNewAsync(objNewItem, this, token: token).ConfigureAwait(false));
                        _indexComparer.Reset(Contents);
                        lstToRedraw = _lstContentList.Skip(e.OldStartingIndex);
                        break;
                    }
                case NotifyCollectionChangedAction.Move:
                    {
                        // Refresh the underlying lists, but do not refresh any displays
                        int intNewIndex = e.NewStartingIndex;
                        int intOldIndex = e.OldStartingIndex;
                        int intDirection = intOldIndex < intNewIndex ? 1 : -1;
                        ControlWithMetaData objMovedControl = _lstContentList[intOldIndex];
                        int intLoopDisplayIndex = _lstDisplayIndex.IndexOf(intOldIndex);
                        int intFinalDisplayIndexValue = intLoopDisplayIndex >= 0 ? intNewIndex : -1;
                        bool blnFinalRenderedValue = intLoopDisplayIndex >= 0 && _ablnRendered[intLoopDisplayIndex];
                        for (int i = intOldIndex; i * intDirection < intNewIndex * intDirection; i += intDirection)
                        {
                            _lstContentList[i] = _lstContentList[i + intDirection];
                            int intDisplayIndex = _lstDisplayIndex.IndexOf(i + intDirection);
                            if (intDisplayIndex != -1)
                            {
                                if (intLoopDisplayIndex != -1)
                                {
                                    _lstDisplayIndex[intLoopDisplayIndex] = _lstDisplayIndex[intDisplayIndex];
                                    _ablnRendered[intLoopDisplayIndex] = _ablnRendered[intDisplayIndex];
                                }
                                else
                                {
                                    intFinalDisplayIndexValue = i;
                                    blnFinalRenderedValue = _ablnRendered[intDisplayIndex];
                                }
                                intLoopDisplayIndex = intDisplayIndex;
                            }
                        }
                        _lstContentList[intNewIndex] = objMovedControl;
                        if (intLoopDisplayIndex != -1)
                        {
                            _lstDisplayIndex[intLoopDisplayIndex] = intFinalDisplayIndexValue;
                            _ablnRendered[intLoopDisplayIndex] = blnFinalRenderedValue;
                        }
                        return;
                    }
                case NotifyCollectionChangedAction.Reset:
                    {
                        if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                            await pnlDisplay.DoThreadSafeAsync(x => x.SuspendLayout(), token: token).ConfigureAwait(false);
                        try
                        {
                            foreach (ControlWithMetaData objLoopControl in _lstContentList)
                            {
                                await objLoopControl.CleanupAsync(token).ConfigureAwait(false);
                            }

                            List<Control> lstControls = new List<Control>(_lstContentList.Count);
                            _lstContentList.Clear();
                            await Contents.ForEachWithSideEffectsAsync(async objLoopTType =>
                            {
                                ControlWithMetaData objControlWithMetadata =
                                    await ControlWithMetaData.GetNewAsync(objLoopTType, this, false, token)
                                        .ConfigureAwait(false);
                                _lstContentList.Add(objControlWithMetadata);
                                lstControls.Add(await objControlWithMetadata.GetControlAsync(token)
                                    .ConfigureAwait(false));
                            }, token: token).ConfigureAwait(false);

                            await pnlDisplay.DoThreadSafeAsync(x => x.Controls.AddRange(lstControls.ToArray()), token: token).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                                await pnlDisplay.DoThreadSafeAsync(x => x.ResumeLayout(), token: token).ConfigureAwait(false);
                        }

                        _indexComparer.Reset(Contents);
                        lstToRedraw = _lstContentList;
                        break;
                    }
            }
            if (lstToRedraw != null)
            {
                if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                    await pnlDisplay.DoThreadSafeAsync(x => x.SuspendLayout(), token: token).ConfigureAwait(false);
                try
                {
                    PropertyChangedEventArgs objArgs = new PropertyChangedEventArgs(nameof(Contents));
                    if (_setChildPropertyChangedAsync.Count > 0)
                    {
                        List<Task> lstTasks =
                            new List<Task>(Math.Min(_setChildPropertyChangedAsync.Count, Utils.MaxParallelBatchSize));
                        int i = 0;
                        foreach (PropertyChangedAsyncEventHandler objEvent in _setChildPropertyChangedAsync)
                        {
                            lstTasks.Add(objEvent.Invoke(this, objArgs, token));
                            if (++i < Utils.MaxParallelBatchSize)
                                continue;
                            await Task.WhenAll(lstTasks).ConfigureAwait(false);
                            lstTasks.Clear();
                            i = 0;
                        }
                        await Task.WhenAll(lstTasks).ConfigureAwait(false);
                    }
                    if (ChildPropertyChanged != null)
                        await Utils.RunOnMainThreadAsync(() => ChildPropertyChanged?.Invoke(this, objArgs), token: token).ConfigureAwait(false);

                    await RedrawControlsAsync(lstToRedraw, token).ConfigureAwait(false);
                }
                finally
                {
                    if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                        await pnlDisplay.DoThreadSafeAsync(x => x.ResumeLayout(), token: token).ConfigureAwait(false);
                }
            }
        }

        private void ObservableCollectionDisplay_Scroll(object sender, ScrollEventArgs e)
        {
            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                pnlDisplay.SuspendLayout();
            try
            {
                LoadScreenContent();
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    pnlDisplay.ResumeLayout();
            }
        }

        private void ObservableCollectionDisplay_SizeChanged(object sender, EventArgs e)
        {
            if (Interlocked.Increment(ref _intSuspendLayoutCount) == 1)
                pnlDisplay.SuspendLayout();
            try
            {
                pnlDisplay.Width = Width - SystemInformation.VerticalScrollBarWidth;
                ResetDisplayPanelHeight();
                foreach (Control control in pnlDisplay.Controls)
                {
                    if (control.AutoSize)
                    {
                        if (control.MinimumSize.Width > pnlDisplay.Width)
                            control.MinimumSize = new Size(pnlDisplay.Width, control.MinimumSize.Height);
                        if (control.MaximumSize.Width != pnlDisplay.Width)
                            control.MaximumSize = new Size(pnlDisplay.Width, control.MaximumSize.Height);
                        if (control.MinimumSize.Width < pnlDisplay.Width)
                            control.MinimumSize = new Size(pnlDisplay.Width, control.MinimumSize.Height);
                    }
                    else
                    {
                        control.Width = pnlDisplay.Width;
                    }
                }
                // Needed for safety reasons in case control size is decreased horizontally
                pnlDisplay.Width = Width - SystemInformation.VerticalScrollBarWidth;
            }
            finally
            {
                if (Interlocked.Decrement(ref _intSuspendLayoutCount) == 0)
                    pnlDisplay.ResumeLayout();
            }
        }

        private sealed class ControlWithMetaData : IDisposable, IAsyncDisposable
        {
            public TType Item { get; }

            public Control Control => _control ?? CreateControl();

            public Task<Control> GetControlAsync(CancellationToken token = default)
            {
                if (token.IsCancellationRequested)
                    return Task.FromCanceled<Control>(token);
                Control objControl = _control;
                return objControl != null ? Task.FromResult(objControl) : CreateControlAsync(token: token);
            }

            public bool Visible => _visible ?? (_visible = _parent._visibleFilter(Item)).Value;

            private readonly ObservableCollectionDisplay<TType> _parent;
            private Control _control;
            private bool? _visible;

            public ControlWithMetaData(TType item, ObservableCollectionDisplay<TType> parent, bool blnAddControlAfterCreation) : this(item, parent)
            {
                // Because binding list displays generally involve syncing the name label of child controls after-the-fact,
                // we need to create the control in the constructor (even if it isn't rendered) so that we can measure its
                // elements' widths and/or heights
                CreateControl(blnAddControlAfterCreation);
                if (item is INotifyPropertyChangedAsync objItem)
                {
                    if (objItem is IHasLockObject objHasLock)
                    {
                        try
                        {
                            using (objHasLock.LockObject.EnterWriteLock())
                                objItem.PropertyChangedAsync += item_ChangedEventAsync;
                        }
                        catch (ObjectDisposedException)
                        {
                            // swallow this
                        }
                    }
                    else
                        objItem.PropertyChangedAsync += item_ChangedEventAsync;
                }
                else if (item is INotifyPropertyChanged prop)
                {
                    if (prop is IHasLockObject objHasLock)
                    {
                        try
                        {
                            using (objHasLock.LockObject.EnterWriteLock())
                                prop.PropertyChanged += item_ChangedEvent;
                        }
                        catch (ObjectDisposedException)
                        {
                            // swallow this
                        }
                    }
                    else
                        prop.PropertyChanged += item_ChangedEvent;
                }
            }

            private ControlWithMetaData(TType item, ObservableCollectionDisplay<TType> parent)
            {
                _parent = parent;
                Item = item;
            }

            public static async Task<ControlWithMetaData> GetNewAsync(TType item,
                ObservableCollectionDisplay<TType> parent, bool blnAddControlAfterCreation = true, CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                ControlWithMetaData objReturn = new ControlWithMetaData(item, parent);
                try
                {
                    await objReturn.CreateControlAsync(blnAddControlAfterCreation, token).ConfigureAwait(false);
                    if (item is INotifyPropertyChangedAsync objItem)
                    {
                        if (objItem is IHasLockObject objHasLock)
                        {
                            try
                            {
                                IAsyncDisposable objLocker = await objHasLock.LockObject.EnterWriteLockAsync(token)
                                    .ConfigureAwait(false);
                                try
                                {
                                    token.ThrowIfCancellationRequested();
                                    objItem.PropertyChangedAsync += objReturn.item_ChangedEventAsync;
                                }
                                finally
                                {
                                    await objLocker.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // swallow this
                            }
                        }
                        else
                            objItem.PropertyChangedAsync += objReturn.item_ChangedEventAsync;
                    }
                    else if (item is INotifyPropertyChanged objItem2)
                    {
                        if (objItem2 is IHasLockObject objHasLock)
                        {
                            try
                            {
                                IAsyncDisposable objLocker = await objHasLock.LockObject.EnterWriteLockAsync(token)
                                    .ConfigureAwait(false);
                                try
                                {
                                    token.ThrowIfCancellationRequested();
                                    objItem2.PropertyChanged += objReturn.item_ChangedEvent;
                                }
                                finally
                                {
                                    await objLocker.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // swallow this
                            }
                        }
                        else
                            objItem2.PropertyChanged += objReturn.item_ChangedEvent;
                    }
                }
                catch
                {
                    await objReturn.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

                return objReturn;
            }

            private void item_ChangedEvent(object sender, PropertyChangedEventArgs e)
            {
                bool changes = false;
                if (_visible != null && _visible.Value != _parent._visibleFilter(Item))
                {
                    changes = true;
                    _visible = !_visible;
                }

                if (_parent._setChildPropertyChangedAsync.Count > 0)
                {
                    List<Func<Task>> lstFuncs = new List<Func<Task>>(_parent._setChildPropertyChangedAsync.Count);
                    foreach (PropertyChangedAsyncEventHandler objEvent in _parent._setChildPropertyChangedAsync)
                        lstFuncs.Add(() => objEvent.Invoke(sender, e));
                    Utils.RunWithoutThreadLock(lstFuncs);
                }
                if (_parent.ChildPropertyChanged != null)
                    Utils.RunOnMainThread(() => _parent.ChildPropertyChanged?.Invoke(sender, e));
                if (changes)
                    _parent.RedrawControls(this.Yield());
            }

            private async Task item_ChangedEventAsync(object sender, PropertyChangedEventArgs e, CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                bool changes = false;
                if (_visible != null && _visible.Value != await _parent._visibleFilterAsync(Item, token).ConfigureAwait(false))
                {
                    changes = true;
                    _visible = !_visible;
                }

                if (_parent._setChildPropertyChangedAsync.Count > 0)
                {
                    List<Task> lstTasks =
                        new List<Task>(Math.Min(_parent._setChildPropertyChangedAsync.Count, Utils.MaxParallelBatchSize));
                    int i = 0;
                    foreach (PropertyChangedAsyncEventHandler objEvent in _parent._setChildPropertyChangedAsync)
                    {
                        lstTasks.Add(objEvent.Invoke(this, e, token));
                        if (++i < Utils.MaxParallelBatchSize)
                            continue;
                        await Task.WhenAll(lstTasks).ConfigureAwait(false);
                        lstTasks.Clear();
                        i = 0;
                    }
                    await Task.WhenAll(lstTasks).ConfigureAwait(false);
                }
                if (_parent.ChildPropertyChanged != null)
                    await Utils.RunOnMainThreadAsync(() => _parent.ChildPropertyChanged?.Invoke(sender, e), token).ConfigureAwait(false);
                if (changes)
                    await _parent.RedrawControlsAsync(this.Yield(), token).ConfigureAwait(false);
            }

            /// <summary>
            /// Updates the height of the contained control if it exists. Necessary to ensure that the contained control doesn't get created prematurely.
            /// </summary>
            public void UpdateHeight()
            {
                _control?.DoThreadSafe(x =>
                {
                    int intParentControlHeight = _parent.ListItemControlHeight;
                    if (x.AutoSize)
                    {
                        if (x.MinimumSize.Height > intParentControlHeight)
                            x.MinimumSize = new Size(x.MinimumSize.Width, intParentControlHeight);
                        if (x.MaximumSize.Height != intParentControlHeight)
                            x.MaximumSize = new Size(x.MaximumSize.Width, intParentControlHeight);
                        if (x.MinimumSize.Height < intParentControlHeight)
                            x.MinimumSize = new Size(x.MinimumSize.Width, intParentControlHeight);
                    }
                    else
                    {
                        x.Height = intParentControlHeight;
                    }
                });
            }

            /// <summary>
            /// Updates the height of the contained control if it exists. Necessary to ensure that the contained control doesn't get created prematurely.
            /// </summary>
            public async Task UpdateHeightAsync(CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                Control objControl = _control;
                if (objControl != null)
                {
                    await objControl.DoThreadSafeAsync(x =>
                    {
                        int intParentControlHeight = _parent.ListItemControlHeight;
                        if (x.AutoSize)
                        {
                            if (x.MinimumSize.Height > intParentControlHeight)
                                x.MinimumSize = new Size(x.MinimumSize.Width, intParentControlHeight);
                            if (x.MaximumSize.Height != intParentControlHeight)
                                x.MaximumSize = new Size(x.MaximumSize.Width, intParentControlHeight);
                            if (x.MinimumSize.Height < intParentControlHeight)
                                x.MinimumSize = new Size(x.MinimumSize.Width, intParentControlHeight);
                        }
                        else
                        {
                            x.Height = intParentControlHeight;
                        }
                    }, token: token).ConfigureAwait(false);
                }
            }

            private Control CreateControl(bool blnAddControlAfterCreation = true)
            {
                Control objNewControl = _parent.DoThreadSafeFunc(x => x._funcCreateControl(Item));
                Control objOldControl = Interlocked.CompareExchange(ref _control, objNewControl, null);
                if (objOldControl != null)
                {
                    objNewControl.DoThreadSafe(x => x.Dispose());
                    objNewControl = objOldControl;
                }
                int intHeight = objNewControl.DoThreadSafeFunc(x => x.PreferredSize.Height);
                objNewControl.DoThreadSafe(x =>
                {
                    x.SuspendLayout();
                    try
                    {
                        x.Visible = false;
                        intHeight = Math.Max(_parent.ListItemControlHeight, intHeight);
                        int intWidth = _parent.DisplayPanel.DoThreadSafeFunc(y => y.Width);
                        if (x.AutoSize)
                        {
                            x.MinimumSize = new Size(intWidth, intHeight);
                            x.MaximumSize = new Size(intWidth, intHeight);
                        }
                        else
                        {
                            x.Width = intWidth;
                            x.Height = intHeight;
                        }
                    }
                    finally
                    {
                        x.ResumeLayout();
                    }
                });
                _parent.ListItemControlHeight = intHeight;
                if (blnAddControlAfterCreation)
                    _parent.DisplayPanel.DoThreadSafe(x => x.Controls.Add(objNewControl));
                return objNewControl;
            }

            private async Task<Control> CreateControlAsync(bool blnAddControlAfterCreation = true, CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                Control objNewControl = await _parent.DoThreadSafeFuncAsync(x => x._funcCreateControl(Item), token: token).ConfigureAwait(false);
                Control objOldControl = Interlocked.CompareExchange(ref _control, objNewControl, null);
                if (objOldControl != null)
                {
                    await objNewControl.DoThreadSafeAsync(x => x.Dispose(), token: token).ConfigureAwait(false);
                    objNewControl = objOldControl;
                }
                int intHeight = await objNewControl.DoThreadSafeFuncAsync(x => x.PreferredSize.Height, token: token).ConfigureAwait(false);
                await objNewControl.DoThreadSafeAsync(x =>
                {
                    x.SuspendLayout();
                    try
                    {
                        x.Visible = false;
                        intHeight = Math.Max(_parent.ListItemControlHeight, intHeight);
                        int intWidth = _parent.DisplayPanel.DoThreadSafeFunc(y => y.Width, token);
                        if (x.AutoSize)
                        {
                            x.MinimumSize = new Size(intWidth, intHeight);
                            x.MaximumSize = new Size(intWidth, intHeight);
                        }
                        else
                        {
                            x.Width = intWidth;
                            x.Height = intHeight;
                        }
                    }
                    finally
                    {
                        x.ResumeLayout();
                    }
                }, token: token).ConfigureAwait(false);
                await _parent.SetListItemControlHeightAsync(intHeight, token).ConfigureAwait(false);
                if (blnAddControlAfterCreation)
                    await _parent.DisplayPanel.DoThreadSafeAsync(x => x.Controls.Add(objNewControl), token: token).ConfigureAwait(false);
                return objNewControl;
            }

            public void RefreshVisible()
            {
                _visible = _parent.DoThreadSafeFunc(x => x._visibleFilter(Item));
                if (!_visible.Value)
                {
                    _control?.DoThreadSafe(x => x.Visible = false);
                }
            }

            public async Task RefreshVisibleAsync(CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                _visible = await _parent._visibleFilterAsync(Item, token).ConfigureAwait(false);
                if (!_visible.Value)
                {
                    Control objControl = _control;
                    if (objControl != null)
                    {
                        await objControl.DoThreadSafeAsync(x => x.Visible = false, token: token).ConfigureAwait(false);
                    }
                }
            }

            public void Reset()
            {
                _visible = null;
                _control?.DoThreadSafe(x =>
                {
                    x.Visible = false;
                    x.Location = new Point(0, 0);
                    int intWidth = _parent.DisplayPanel.DoThreadSafeFunc(y => y.Width);
                    int intHeight = _parent.ListItemControlHeight;
                    if (x.AutoSize)
                    {
                        x.MinimumSize = new Size(intWidth, intHeight);
                        x.MaximumSize = new Size(intWidth, intHeight);
                    }
                    else
                    {
                        x.Width = intWidth;
                        x.Height = intHeight;
                    }
                });
            }

            public async Task ResetAsync(CancellationToken token = default)
            {
                _visible = null;
                Control objControl = _control;
                if (objControl != null)
                {
                    await objControl.DoThreadSafeAsync(x =>
                    {
                        x.Visible = false;
                        x.Location = new Point(0, 0);
                        int intWidth = _parent.DisplayPanel.DoThreadSafeFunc(y => y.Width, token);
                        int intHeight = _parent.ListItemControlHeight;
                        if (x.AutoSize)
                        {
                            x.MinimumSize = new Size(intWidth, intHeight);
                            x.MaximumSize = new Size(intWidth, intHeight);
                        }
                        else
                        {
                            x.Width = intWidth;
                            x.Height = intHeight;
                        }
                    }, token: token).ConfigureAwait(false);
                }
            }

            public void Cleanup()
            {
                Control objControl = Interlocked.Exchange(ref _control, null);
                if (objControl != null)
                {
                    if (Item is INotifyPropertyChangedAsync objItem)
                    {
                        if (objItem is IHasLockObject objHasLock)
                        {
                            try
                            {
                                using (objHasLock.LockObject.EnterWriteLock())
                                    objItem.PropertyChangedAsync -= item_ChangedEventAsync;
                            }
                            catch (ObjectDisposedException)
                            {
                                // swallow this
                            }
                        }
                        else
                            objItem.PropertyChangedAsync -= item_ChangedEventAsync;
                    }
                    else if (Item is INotifyPropertyChanged objItem2)
                    {
                        if (objItem2 is IHasLockObject objHasLock)
                        {
                            try
                            {
                                using (objHasLock.LockObject.EnterWriteLock())
                                    objItem2.PropertyChanged -= item_ChangedEvent;
                            }
                            catch (ObjectDisposedException)
                            {
                                // swallow this
                            }
                        }
                        else
                            objItem2.PropertyChanged -= item_ChangedEvent;
                    }
                    _parent.DisplayPanel.DoThreadSafe(x => x.Controls.Remove(objControl));
                    objControl.DoThreadSafe(x => x.Dispose());
                }
            }

            public async Task CleanupAsync(CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                Control objControl = Interlocked.Exchange(ref _control, null);
                if (objControl != null)
                {
                    if (Item is INotifyPropertyChangedAsync objItem)
                    {
                        if (objItem is IHasLockObject objHasLock)
                        {
                            try
                            {
                                IAsyncDisposable objLocker = await objHasLock.LockObject.EnterWriteLockAsync(token)
                                    .ConfigureAwait(false);
                                try
                                {
                                    token.ThrowIfCancellationRequested();
                                    objItem.PropertyChangedAsync -= item_ChangedEventAsync;
                                }
                                finally
                                {
                                    await objLocker.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // swallow this
                            }
                        }
                        else
                            objItem.PropertyChangedAsync -= item_ChangedEventAsync;
                    }
                    else if (Item is INotifyPropertyChanged objItem2)
                    {
                        if (objItem2 is IHasLockObject objHasLock)
                        {
                            try
                            {
                                IAsyncDisposable objLocker = await objHasLock.LockObject.EnterWriteLockAsync(token)
                                    .ConfigureAwait(false);
                                try
                                {
                                    token.ThrowIfCancellationRequested();
                                    objItem2.PropertyChanged -= item_ChangedEvent;
                                }
                                finally
                                {
                                    await objLocker.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // swallow this
                            }
                        }
                        else
                            objItem2.PropertyChanged -= item_ChangedEvent;
                    }

                    await _parent.DisplayPanel.DoThreadSafeAsync(x => x.Controls.Remove(objControl), token: token).ConfigureAwait(false);
                    await objControl.DoThreadSafeAsync(x => x.Dispose(), token: token).ConfigureAwait(false);
                }
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _control, null)?.DoThreadSafe(x => x.Dispose());
            }

            public async ValueTask DisposeAsync()
            {
                Control objControl = Interlocked.Exchange(ref _control, null);
                if (objControl != null)
                    await objControl.DoThreadSafeAsync(x => x.Dispose()).ConfigureAwait(false);
            }
        }

        private sealed class IndexComparer : IComparer<TType>
        {
            private readonly ConcurrentDictionary<TType, int> _dicIndeces = new ConcurrentDictionary<TType, int>();

            public int Compare(TType x, TType y)
            {
                if (!Equals(x, default(TType)) && _dicIndeces.TryGetValue(x, out int xindex))
                {
                    if (!Equals(y, default(TType)) && _dicIndeces.TryGetValue(y, out int yindex))
                    {
                        return xindex.CompareTo(yindex);
                    }

                    Utils.BreakIfDebug();
                    return 1;
                }

                Utils.BreakIfDebug();
                if (!Equals(y, default(TType)) && (Equals(x, default(TType)) || _dicIndeces.ContainsKey(y)))
                    return -1;

                return 0;
            }

            public IndexComparer(IReadOnlyList<TType> list)
            {
                Reset(list);
            }

            public void Reset(IReadOnlyList<TType> source)
            {
                _dicIndeces.Clear();
                for (int i = 0; i < source.Count; i++)
                {
                    _dicIndeces.AddOrUpdate(source[i], i, (x, y) => i);
                }
            }
        }

        private static Task<int> DefaultCompareAsync(IComparer<TType> objComparer, TType x, TType y, CancellationToken token = default)
        {
            return token.IsCancellationRequested
                ? Task.FromCanceled<int>(token)
                : Task.FromResult(objComparer.Compare(x, y));
        }

        private static Task<bool> DefaultVisibleAsync(TType x, CancellationToken token = default)
        {
            return token.IsCancellationRequested ? Task.FromCanceled<bool>(token) : Task.FromResult(true);
        }

        private static Task<bool> DefaultVisibleAsync(Predicate<TType> predicate, TType x, CancellationToken token = default)
        {
            return token.IsCancellationRequested
                ? Task.FromCanceled<bool>(token)
                : Task.FromResult(predicate.Invoke(x));
        }

        private async void ObservableCollectionDisplay_DpiChangedAfterParent(object sender, EventArgs e)
        {
            int intMaxControlHeight = 0;
            foreach (ControlWithMetaData objControl in _lstContentList)
            {
                intMaxControlHeight = Math.Max(await (await objControl.GetControlAsync().ConfigureAwait(false)).DoThreadSafeFuncAsync(x => x.PreferredSize.Height).ConfigureAwait(false), intMaxControlHeight);
            }

            await SetListItemControlHeightAsync(intMaxControlHeight).ConfigureAwait(false);
        }
    }
}

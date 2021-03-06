using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Sidekick.Business.Apis.Poe.Models;
using Sidekick.Business.Apis.PoePriceInfo.Models;
using Sidekick.Business.Categories;
using Sidekick.Business.Languages;
using Sidekick.Business.Trades.Results;
using Sidekick.Core.Natives;
using Sidekick.Windows.PriceCheck.ViewModels;

namespace Sidekick.Windows.PriceCheck
{
    public partial class OverlayWindow : Window, INotifyPropertyChanged
    {
        #region Events
        public delegate void ItemScrollReachedEndHandler(Business.Parsers.Models.Item item, int displayedItemsCount);
        public event ItemScrollReachedEndHandler ItemScrollReachedEnd;
        public void OnItemScrollReachedEnd(Business.Parsers.Models.Item item, int displayedItemsCount)
        {
            ItemScrollReachedEnd?.Invoke(item, displayedItemsCount);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
        public ObservableCollection<ListItem> itemListingControls { get; set; } = new ObservableCollection<ListItem>();

        private QueryResult<ListingResult> queryResultValue = new QueryResult<ListingResult>();
        public QueryResult<ListingResult> queryResult
        {
            get { return queryResultValue; }
            set { queryResultValue = value; NotifyPropertyChanged(); }
        }

        private bool overlayIsUpdatable = false;
        private bool dataIsUpdating = false;
        private readonly IPoePriceInfoClient poePriceInfoClient;
        private readonly INativeBrowser nativeBrowser;
        private readonly ILanguageProvider languageProvider;
        private readonly IStaticItemCategoryService staticItemCategoryService;

        public OverlayWindow(
            IPoePriceInfoClient poePriceInfoClient,
            INativeBrowser nativeBrowser,
            ILanguageProvider languageProvider,
            IStaticItemCategoryService staticItemCategoryService)
        {
            this.poePriceInfoClient = poePriceInfoClient;
            this.nativeBrowser = nativeBrowser;
            this.languageProvider = languageProvider;
            this.staticItemCategoryService = staticItemCategoryService;
            InitializeComponent();
            DataContext = this;
            Hide();
            Loaded += OverlayWindow_Loaded;
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = _itemList.GetChildOfType<ScrollViewer>();
            scrollViewer?.ScrollToTop();
            scrollViewer.ScrollChanged += itemList_ScrollChanged;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        public bool IsDisplayed => Visibility == Visibility.Visible;

        public void SetQueryResult(QueryResult<ListingResult> queryResult)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new SetQueryResultCallback(SetQueryResult), new object[] { queryResult });
            }
            else
            {
                if (!IsDisplayed)
                {
                    return;
                }

                this.queryResult = queryResult;
                itemListingControls?.Clear();

                AppendToItemListing(queryResult.Result);

                // Hardcoded to the English value of Rare since poeprices.info only support English.
                if (queryResult.Item.Rarity == "Rare" && queryResult.Item.IsIdentified)
                {
                    Task.Run(() => GetPricePrediction(queryResult.Item.ItemText));
                }
            }
        }
        delegate void SetQueryResultCallback(QueryResult<ListingResult> queryToAppend);

        private async Task GetPricePrediction(string itemText)
        {
            var predictionResult = await poePriceInfoClient.GetItemPricePrediction(itemText);
            if (predictionResult?.ErrorCode != 0)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                txtPrediction.Text = $"{predictionResult.Min?.ToString("F")}-{predictionResult.Max?.ToString("F")} {predictionResult.Currency} ({predictionResult.ConfidenceScore.ToString("N1")}%)";
            });
        }

        public void AppendQueryResult(QueryResult<ListingResult> queryToAppend)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new AppendQueryResultCallback(SetQueryResult), new object[] { queryToAppend });
            }
            else
            {
                if (!IsDisplayed)
                {
                    return;
                }

                // Update queryResult
                var newQueryResult = new QueryResult<ListingResult>
                {
                    Id = queryResult.Id,
                    Item = queryResult.Item,
                    Total = queryToAppend.Total,
                    Uri = queryResult.Uri,
                    PoeNinjaItem = queryResult.PoeNinjaItem
                };

                var newResults = new List<ListingResult>();
                newResults.AddRange(queryResult.Result);
                newResults.AddRange(queryToAppend.Result);
                newQueryResult.Result = newResults;

                queryResult = newQueryResult;
                AppendToItemListing(queryToAppend.Result);

                dataIsUpdating = false;
            }
        }
        delegate void AppendQueryResultCallback(QueryResult<ListingResult> queryResult);

        private void AppendToItemListing(IEnumerable<ListingResult> listingResults)
        {
            foreach (var listingResult in listingResults)
            {
                staticItemCategoryService.CurrencyUrls.TryGetValue(listingResult.Listing.Price.Currency, out var url);

                // Find a cleaner way of getting the PoeCdnBaseUrl?
                itemListingControls?.Add(new ListItem(listingResult, $"{languageProvider.Language.PoeCdnBaseUrl}{url}"));
            }
        }

        public void SetWindowPosition(int x, int y)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new SetWindowPositionCallback(SetWindowPosition), new object[] { x, y });
            }
            else
            {
                Left = x;
                Top = y;
            }
        }
        delegate void SetWindowPositionCallback(int x, int y);

        public void ShowWindow()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new ShowWindowCallback(ShowWindow));
            }
            else
            {
                itemListingControls?.Clear();

                txtPrediction.Text = null;
                queryResult = null;
                dataIsUpdating = false;

                Visibility = Visibility.Visible;

                var scrollViewer = _itemList.GetChildOfType<ScrollViewer>();
                scrollViewer?.ScrollToTop();
            }
        }
        delegate void ShowWindowCallback();

        public void HideWindowAndClearData()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new HideWindowAndClearDataCallback(HideWindowAndClearData));
            }
            else
            {
                txtPrediction.Text = null;
                queryResult = null;
                itemListingControls = new ObservableCollection<ListItem>();
                NotifyPropertyChanged("itemListingControls");
                Visibility = Visibility.Hidden;
            }
        }
        delegate void HideWindowAndClearDataCallback();

        private void itemList_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            // The api only returns 100 results maximum.
            if (!itemListingControls.Any() || itemListingControls.Count >= 100)
            {
                return;
            }

            var scrollViewer = _itemList.GetChildOfType<ScrollViewer>();

            //Load next results when scrollviewer is at the bottom
            if (scrollViewer?.ScrollableHeight > 0)
            {
                // Query next page when reaching more than 70% of the scrollable content.
                var breakpoint = (scrollViewer.VerticalOffset / scrollViewer.ScrollableHeight) > 0.7d;
                if (breakpoint && overlayIsUpdatable && !dataIsUpdating)
                {
                    dataIsUpdating = true;
                    overlayIsUpdatable = false;
                    OnItemScrollReachedEnd(queryResult.Item, itemListingControls.Count);
                    return;
                }
            }

            // UI update is finished, when the scrollviewer is reset (newly added items will move the scrollbar)
            overlayIsUpdatable = true;
        }

        private void openQueryLink(object sender, RequestNavigateEventArgs e)
        {
            nativeBrowser.Open(e.Uri);
            e.Handled = true;
        }
    }
}

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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using iText.StyledXmlParser.Jsoup.Parser;
using NLog;

namespace Chummer.Backend.Equipment
{
    /// <summary>
    /// Weapon Accessory.
    /// </summary>
    [DebuggerDisplay("{DisplayName(GlobalSettings.DefaultLanguage)}")]
    public sealed class WeaponAccessory : IHasInternalId, IHasName, IHasSourceId, IHasXmlDataNode, IHasNotes, ICanSell, ICanEquip, IHasSource, IHasRating, ICanSort, IHasWirelessBonus, IHasStolenProperty, ICanPaste, IHasGear, ICanBlackMarketDiscount, IDisposable, IAsyncDisposable, IHasCharacterObject
    {
        private static readonly Lazy<Logger> s_ObjLogger = new Lazy<Logger>(LogManager.GetCurrentClassLogger);
        private static Logger Log => s_ObjLogger.Value;
        private Guid _guiID;
        private Guid _guiSourceID;
        private readonly Character _objCharacter;
        private XmlNode _nodAllowGear;
        private readonly TaggedObservableCollection<Gear> _lstGear;
        private Weapon _objParent;
        private string _strName = string.Empty;
        private string _strMount = string.Empty;
        private string _strExtraMount = string.Empty;
        private string _strAddMount = string.Empty;
        private string _strRC = string.Empty;
        private string _strDamage = string.Empty;
        private string _strDamageType = string.Empty;
        private string _strDamageReplace = string.Empty;
        private string _strFireMode = string.Empty;
        private string _strFireModeReplace = string.Empty;
        private string _strAPReplace = string.Empty;
        private string _strAP = string.Empty;
        private string _strConceal = string.Empty;
        private string _strAvail = string.Empty;
        private string _strCost = string.Empty;
        private string _strWeight = string.Empty;
        private string _strSource = string.Empty;
        private string _strPage = string.Empty;
        private string _strNotes = string.Empty;
        private Color _colNotes = ColorManager.HasNotesColor;
        private string _strDicePool = string.Empty;
        private string _strRatingLabel = "String_Rating";
        private string _strAccuracy;
        private string _strMaxRating;
        private int _intRating;
        private int _intRCGroup;
        private string _strReach;
        private int _intAmmoSlots;
        private string _strModifyAmmoCapacity;
        private bool _blnDeployable;
        private bool _blnDiscountCost;
        private bool _blnIncludedInWeapon;
        private bool _blnSpecialModification;
        private bool _blnEquipped = true;
        private int _intAccessoryCostMultiplier = 1;
        private string _strExtra = string.Empty;
        private string _strReplaceRange = string.Empty;
        private string _strRangeBonus = "0";
        private string _strRangeModifier = "0";
        private int _intSingleShot;
        private int _intShortBurst;
        private int _intLongBurst;
        private int _intFullBurst;
        private int _intSuppressive;
        private string _strAmmoReplace = string.Empty;
        private string _strAmmoBonus = string.Empty;
        private int _intSortOrder;
        private bool _blnWirelessOn = true;
        private XmlElement _nodWirelessBonus;
        private XmlElement _nodWirelessWeaponBonus;
        private bool _blnStolen;
        private string _strParentID;

        #region Constructor, Create, Save, Load, and Print Methods

        public WeaponAccessory(Character objCharacter)
        {
            // Create the GUID for the new Weapon.
            _guiID = Guid.NewGuid();
            _objCharacter = objCharacter;
            _lstGear = new TaggedObservableCollection<Gear>(objCharacter.LockObject);
            _lstGear.AddTaggedCollectionChanged(this, GearChildrenOnCollectionChanged);
        }

        private async Task GearChildrenOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            bool blnDoEquipped = _objCharacter?.IsLoading == false && Equipped && Parent?.ParentVehicle == null;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (Gear objNewItem in e.NewItems)
                    {
                        await objNewItem.SetParentAsync(this, token).ConfigureAwait(false);
                        if (blnDoEquipped)
                            await objNewItem.ChangeEquippedStatusAsync(true, token: token).ConfigureAwait(false);
                    }

                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (Gear objOldItem in e.OldItems)
                    {
                        await objOldItem.SetParentAsync(null, token).ConfigureAwait(false);
                        if (blnDoEquipped)
                            await objOldItem.ChangeEquippedStatusAsync(false, token: token).ConfigureAwait(false);
                    }

                    break;

                case NotifyCollectionChangedAction.Replace:
                    foreach (Gear objOldItem in e.OldItems)
                    {
                        await objOldItem.SetParentAsync(null, token).ConfigureAwait(false);
                        if (blnDoEquipped)
                            await objOldItem.ChangeEquippedStatusAsync(false, token: token).ConfigureAwait(false);
                    }

                    foreach (Gear objNewItem in e.NewItems)
                    {
                        await objNewItem.SetParentAsync(this, token).ConfigureAwait(false);
                        if (blnDoEquipped)
                            await objNewItem.ChangeEquippedStatusAsync(true, token: token).ConfigureAwait(false);
                    }

                    break;

                case NotifyCollectionChangedAction.Reset:
                    if (blnDoEquipped)
                        await _objCharacter.OnPropertyChangedAsync(nameof(Character.TotalCarriedWeight), token).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        /// Create a Weapon Accessory from an XmlNode and return the TreeNodes for it.
        /// </summary>
        /// <param name="objXmlAccessory">XmlNode to create the object from.</param>
        /// <param name="strMount">Mount slot that the Weapon Accessory will consume.</param>
        /// <param name="intRating">Rating of the Weapon Accessory.</param>
        /// <param name="blnCreateChildren">Whether child items should be created.</param>
        /// <param name="blnCreateImprovements">Whether bonuses should be created.</param>
        /// <param name="blnSkipCost">Whether forms asking to determine variable costs should be displayed.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        public void Create(XmlNode objXmlAccessory, Tuple<string, string> strMount, int intRating,
            bool blnSkipCost = false, bool blnCreateChildren = true, bool blnCreateImprovements = true, CancellationToken token = default)
        {
            Utils.SafelyRunSynchronously(() => CreateCoreAsync(true, objXmlAccessory, strMount, intRating, blnSkipCost,
                blnCreateChildren, blnCreateImprovements, token), token);
        }

        /// <summary>
        /// Create a Weapon Accessory from an XmlNode and return the TreeNodes for it.
        /// </summary>
        /// <param name="objXmlAccessory">XmlNode to create the object from.</param>
        /// <param name="strMount">Mount slot that the Weapon Accessory will consume.</param>
        /// <param name="intRating">Rating of the Weapon Accessory.</param>
        /// <param name="blnCreateChildren">Whether child items should be created.</param>
        /// <param name="blnCreateImprovements">Whether bonuses should be created.</param>
        /// <param name="blnSkipCost">Whether forms asking to determine variable costs should be displayed.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        public Task CreateAsync(XmlNode objXmlAccessory, Tuple<string, string> strMount, int intRating,
            bool blnSkipCost = false, bool blnCreateChildren = true, bool blnCreateImprovements = true, CancellationToken token = default)
        {
            return CreateCoreAsync(false, objXmlAccessory, strMount, intRating, blnSkipCost, blnCreateChildren,
                blnCreateImprovements, token);
        }

        private async Task CreateCoreAsync(bool blnSync, XmlNode objXmlAccessory, Tuple<string, string> strMount, int intRating,
            bool blnSkipCost = false, bool blnCreateChildren = true, bool blnCreateImprovements = true, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!objXmlAccessory.TryGetField("id", Guid.TryParse, out _guiSourceID))
            {
                Log.Warn(new object[] { "Missing id field for weapon accessory xmlnode", objXmlAccessory });
                Utils.BreakIfDebug();
            }
            else
            {
                _objCachedMyXmlNode = null;
                _objCachedMyXPathNode = null;
            }

            _blnEquipped = blnCreateImprovements;
            objXmlAccessory.TryGetStringFieldQuickly("name", ref _strName);
            _strMount = strMount?.Item1 ?? string.Empty;
            _strExtraMount = strMount?.Item2 ?? string.Empty;
            objXmlAccessory.TryGetStringFieldQuickly("addmount", ref _strAddMount);
            objXmlAccessory.TryGetStringFieldQuickly("rating", ref _strMaxRating);
            _intRating = intRating; // Set first to make MaxRatingValue work properly
            _intRating = Math.Min(intRating, blnSync ? MaxRatingValue : await GetMaxRatingValueAsync(token).ConfigureAwait(false));
            objXmlAccessory.TryGetStringFieldQuickly("ratinglabel", ref _strRatingLabel);
            objXmlAccessory.TryGetStringFieldQuickly("avail", ref _strAvail);
            objXmlAccessory.TryGetStringFieldQuickly("weight", ref _strWeight);
            // Check for a Variable Cost.
            if (blnSkipCost)
                _strCost = "0";
            else
            {
                if (!objXmlAccessory.TryGetStringFieldQuickly("cost", ref _strCost))
                    _strCost = "0";
                if (_strCost.StartsWith("Variable(", StringComparison.Ordinal))
                {
                    decimal decMin;
                    decimal decMax = decimal.MaxValue;
                    string strCost = _strCost.TrimStartOnce("Variable(", true).TrimEndOnce(')');
                    if (strCost.Contains('-'))
                    {
                        string[] strValues = strCost.SplitFixedSizePooledArray('-', 2);
                        try
                        {
                            decMin = Convert.ToDecimal(strValues[0], GlobalSettings.InvariantCultureInfo);
                            decMax = Convert.ToDecimal(strValues[1], GlobalSettings.InvariantCultureInfo);
                        }
                        finally
                        {
                            ArrayPool<string>.Shared.Return(strValues);
                        }
                    }
                    else
                        decMin = Convert.ToDecimal(strCost.FastEscape('+'), GlobalSettings.InvariantCultureInfo);

                    if (decMin != 0 || decMax != decimal.MaxValue)
                    {
                        if (decMax > 1000000)
                            decMax = 1000000;

                        if (blnSync)
                        {
                            using (ThreadSafeForm<SelectNumber> frmPickNumber
                                   // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                                   = ThreadSafeForm<SelectNumber>.Get(() => new SelectNumber(_objCharacter.Settings.MaxNuyenDecimals)
                                   {
                                       Minimum = decMin,
                                       Maximum = decMax,
                                       Description = string.Format(
                                           GlobalSettings.CultureInfo,
                                           LanguageManager.GetString("String_SelectVariableCost", token: token),
                                           CurrentDisplayNameShort),
                                       AllowCancel = false
                                   }))
                            {
                                // ReSharper disable once MethodHasAsyncOverload
                                if (frmPickNumber.ShowDialogSafe(_objCharacter, token) == DialogResult.Cancel)
                                {
                                    _guiID = Guid.Empty;
                                    return;
                                }
                                _strCost = frmPickNumber.MyForm.SelectedValue.ToString(GlobalSettings.InvariantCultureInfo);
                            }
                        }
                        else
                        {
                            string strDescription = string.Format(
                                GlobalSettings.CultureInfo,
                                await LanguageManager.GetStringAsync("String_SelectVariableCost", token: token).ConfigureAwait(false),
                                await GetCurrentDisplayNameShortAsync(token).ConfigureAwait(false));
                            int intDecimalPlaces = await (await _objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).GetMaxNuyenDecimalsAsync(token).ConfigureAwait(false);
                            using (ThreadSafeForm<SelectNumber> frmPickNumber
                                   = await ThreadSafeForm<SelectNumber>.GetAsync(() => new SelectNumber(intDecimalPlaces)
                                   {
                                       Minimum = decMin,
                                       Maximum = decMax,
                                       Description = strDescription,
                                       AllowCancel = false
                                   }, token).ConfigureAwait(false))
                            {
                                if (await frmPickNumber.ShowDialogSafeAsync(_objCharacter, token).ConfigureAwait(false) == DialogResult.Cancel)
                                {
                                    _guiID = Guid.Empty;
                                    return;
                                }
                                _strCost = frmPickNumber.MyForm.SelectedValue.ToString(GlobalSettings.InvariantCultureInfo);
                            }
                        }
                    }
                }
            }

            objXmlAccessory.TryGetStringFieldQuickly("source", ref _strSource);
            objXmlAccessory.TryGetStringFieldQuickly("page", ref _strPage);
            _nodAllowGear = objXmlAccessory["allowgear"];
            if (!objXmlAccessory.TryGetMultiLineStringFieldQuickly("altnotes", ref _strNotes))
                objXmlAccessory.TryGetMultiLineStringFieldQuickly("notes", ref _strNotes);

            string sNotesColor = ColorTranslator.ToHtml(ColorManager.HasNotesColor);
            objXmlAccessory.TryGetStringFieldQuickly("notesColor", ref sNotesColor);
            _colNotes = ColorTranslator.FromHtml(sNotesColor);

            if (GlobalSettings.InsertPdfNotesIfAvailable && string.IsNullOrEmpty(Notes))
            {
                if (blnSync)
                    // ReSharper disable once MethodHasAsyncOverload
                    Notes = CommonFunctions.GetBookNotes(objXmlAccessory, Name, CurrentDisplayName, Source,
                        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                        Page, DisplayPage(GlobalSettings.Language), _objCharacter, token);
                else
                    await SetNotesAsync(await CommonFunctions.GetBookNotesAsync(objXmlAccessory, Name,
                        await GetCurrentDisplayNameAsync(token).ConfigureAwait(false), Source, Page,
                        await DisplayPageAsync(GlobalSettings.Language, token).ConfigureAwait(false), _objCharacter, token).ConfigureAwait(false), token).ConfigureAwait(false);
            }

            objXmlAccessory.TryGetStringFieldQuickly("rc", ref _strRC);
            objXmlAccessory.TryGetBoolFieldQuickly("rcdeployable", ref _blnDeployable);
            objXmlAccessory.TryGetInt32FieldQuickly("rcgroup", ref _intRCGroup);
            objXmlAccessory.TryGetStringFieldQuickly("conceal", ref _strConceal);
            objXmlAccessory.TryGetInt32FieldQuickly("ammoslots", ref _intAmmoSlots);
            objXmlAccessory.TryGetStringFieldQuickly("modifyammocapacity", ref _strModifyAmmoCapacity);
            objXmlAccessory.TryGetStringFieldQuickly("ammoreplace", ref _strAmmoReplace);
            objXmlAccessory.TryGetStringFieldQuickly("accuracy", ref _strAccuracy);
            if (_strAccuracy == "0" || _strAccuracy == "+0" || _strAccuracy == "-0")
                _strAccuracy = string.Empty;
            objXmlAccessory.TryGetStringFieldQuickly("dicepool", ref _strDicePool);
            objXmlAccessory.TryGetStringFieldQuickly("damagetype", ref _strDamageType);
            objXmlAccessory.TryGetStringFieldQuickly("damage", ref _strDamage);
            objXmlAccessory.TryGetStringFieldQuickly("damagereplace", ref _strDamageReplace);
            objXmlAccessory.TryGetStringFieldQuickly("firemode", ref _strFireMode);
            objXmlAccessory.TryGetStringFieldQuickly("firemodereplace", ref _strFireModeReplace);
            objXmlAccessory.TryGetStringFieldQuickly("reach", ref _strReach);
            if (_strReach == "0" || _strReach == "+0" || _strReach == "-0")
                _strReach = string.Empty;
            objXmlAccessory.TryGetStringFieldQuickly("ap", ref _strAP);
            objXmlAccessory.TryGetStringFieldQuickly("apreplace", ref _strAPReplace);
            string strTemp = string.Empty;
            if (objXmlAccessory.TryGetStringFieldQuickly("addmode", ref strTemp))
            {
                if (string.IsNullOrEmpty(_strFireMode))
                    _strFireMode = strTemp;
                else if (!_strFireMode.Contains(strTemp))
                    _strFireMode += '/' + strTemp;
            }
            objXmlAccessory.TryGetInt32FieldQuickly("singleshot", ref _intSingleShot);
            objXmlAccessory.TryGetInt32FieldQuickly("shortburst", ref _intShortBurst);
            objXmlAccessory.TryGetInt32FieldQuickly("longburst", ref _intLongBurst);
            objXmlAccessory.TryGetInt32FieldQuickly("fullburst", ref _intFullBurst);
            objXmlAccessory.TryGetInt32FieldQuickly("suppressive", ref _intSuppressive);
            objXmlAccessory.TryGetStringFieldQuickly("replacerange", ref _strReplaceRange);
            objXmlAccessory.TryGetStringFieldQuickly("rangebonus", ref _strRangeBonus);
            objXmlAccessory.TryGetStringFieldQuickly("rangemodifier", ref _strRangeModifier);
            objXmlAccessory.TryGetStringFieldQuickly("extra", ref _strExtra);
            objXmlAccessory.TryGetStringFieldQuickly("ammobonus", ref _strAmmoBonus);
            objXmlAccessory.TryGetInt32FieldQuickly("accessorycostmultiplier", ref _intAccessoryCostMultiplier);
            objXmlAccessory.TryGetBoolFieldQuickly("specialmodification", ref _blnSpecialModification);

            // Add any Gear that comes with the Weapon Accessory.
            XmlElement xmlGearsNode = objXmlAccessory["gears"];
            if (xmlGearsNode != null && blnCreateChildren)
            {
                XmlDocument objXmlGearDocument = blnSync
                    // ReSharper disable once MethodHasAsyncOverload
                    ? _objCharacter.LoadData("gear.xml", token: token)
                    : await _objCharacter.LoadDataAsync("gear.xml", token: token).ConfigureAwait(false);
                using (XmlNodeList xmlGearsList = xmlGearsNode.SelectNodes("usegear"))
                {
                    if (xmlGearsList != null)
                    {
                        foreach (XmlNode objXmlAccessoryGear in xmlGearsList)
                        {
                            XmlElement objXmlAccessoryGearName = objXmlAccessoryGear["name"];
                            XmlElement objXmlAccessoryGearCategory = objXmlAccessoryGear["category"];
                            XmlAttributeCollection objXmlAccessoryGearNameAttributes =
                                objXmlAccessoryGearName?.Attributes;
                            int intGearRating = 0;
                            decimal decGearQty = 1;
                            string strChildForceSource = objXmlAccessoryGear["source"]?.InnerText ?? string.Empty;
                            string strChildForcePage = objXmlAccessoryGear["page"]?.InnerText ?? string.Empty;
                            string strChildForceValue =
                                objXmlAccessoryGearNameAttributes?["select"]?.InnerText ?? string.Empty;
                            bool blnChildCreateChildren =
                                objXmlAccessoryGearNameAttributes?["createchildren"]?.InnerText != bool.FalseString;
                            bool blnAddChildImprovements = blnCreateImprovements &&
                                                           objXmlAccessoryGearNameAttributes?["addimprovements"]
                                                               ?.InnerText != bool.FalseString;
                            if (objXmlAccessoryGear["rating"] != null)
                                intGearRating = Convert.ToInt32(objXmlAccessoryGear["rating"].InnerText,
                                    GlobalSettings.InvariantCultureInfo);
                            if (objXmlAccessoryGearNameAttributes?["qty"] != null)
                            {
                                decimal.TryParse(objXmlAccessoryGearNameAttributes["qty"].InnerText, NumberStyles.Any, GlobalSettings.InvariantCultureInfo, out decGearQty);
                            }
                            string strFilter = "/chummer/gears/gear";
                            if (objXmlAccessoryGearName != null || objXmlAccessoryGearCategory != null)
                            {
                                strFilter += '[';
                                if (objXmlAccessoryGearName != null)
                                {
                                    strFilter += "name = " + objXmlAccessoryGearName.InnerText.CleanXPath();
                                    if (objXmlAccessoryGearCategory != null)
                                        strFilter += " and category = " +
                                                     objXmlAccessoryGearCategory.InnerText.CleanXPath();
                                }
                                else
                                    strFilter += "category = " + objXmlAccessoryGearCategory.InnerText.CleanXPath();

                                strFilter += ']';
                            }

                            XmlNode objXmlGear = objXmlGearDocument.SelectSingleNode(strFilter);

                            Gear objGear = new Gear(_objCharacter);

                            List<Weapon> lstWeapons = new List<Weapon>(1);

                            if (blnSync)
                            {
                                // ReSharper disable once MethodHasAsyncOverload
                                objGear.Create(objXmlGear, intGearRating, lstWeapons, strChildForceValue,
                                    blnAddChildImprovements, blnChildCreateChildren, token: token);
                                objGear.Quantity = decGearQty;
                            }
                            else
                            {
                                await objGear.CreateAsync(objXmlGear, intGearRating, lstWeapons, strChildForceValue,
                                        blnAddChildImprovements, blnChildCreateChildren, token: token)
                                    .ConfigureAwait(false);
                                await objGear.SetQuantityAsync(decGearQty, token).ConfigureAwait(false);
                            }

                            objGear.Cost = "0";
                            objGear.ParentID = InternalId;
                            if (!string.IsNullOrEmpty(strChildForceSource))
                                objGear.Source = strChildForceSource;
                            if (!string.IsNullOrEmpty(strChildForcePage))
                                objGear.Page = strChildForcePage;
                            if (blnSync)
                                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                                _lstGear.Add(objGear);
                            else
                                await _lstGear.AddAsync(objGear, token).ConfigureAwait(false);

                            // Change the Capacity of the child if necessary.
                            if (objXmlAccessoryGear["capacity"] != null)
                                objGear.Capacity = '[' + objXmlAccessoryGear["capacity"].InnerText + ']';
                        }
                    }
                }
            }

            _nodWirelessBonus = objXmlAccessory["wirelessbonus"];
            _nodWirelessWeaponBonus = objXmlAccessory["wirelessweaponbonus"];
        }

        private SourceString _objCachedSourceDetail;

        public SourceString SourceDetail =>
            _objCachedSourceDetail == default
                ? _objCachedSourceDetail = SourceString.GetSourceString(Source,
                    DisplayPage(GlobalSettings.Language),
                    GlobalSettings.Language,
                    GlobalSettings.CultureInfo,
                    _objCharacter)
                : _objCachedSourceDetail;

        public async Task<SourceString> GetSourceDetailAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return _objCachedSourceDetail == default
                ? _objCachedSourceDetail = await SourceString.GetSourceStringAsync(Source,
                    await DisplayPageAsync(GlobalSettings.Language, token).ConfigureAwait(false),
                    GlobalSettings.Language,
                    GlobalSettings.CultureInfo,
                    _objCharacter, token).ConfigureAwait(false)
                : _objCachedSourceDetail;
        }

        /// <summary>
        /// Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlWriter objWriter)
        {
            if (objWriter == null)
                return;
            objWriter.WriteStartElement("accessory");
            objWriter.WriteElementString("sourceid", SourceIDString);
            objWriter.WriteElementString("guid", InternalId);
            objWriter.WriteElementString("name", _strName);
            objWriter.WriteElementString("mount", _strMount);
            objWriter.WriteElementString("extramount", _strExtraMount);
            objWriter.WriteElementString("addmount", _strAddMount);
            objWriter.WriteElementString("rc", _strRC);
            objWriter.WriteElementString("maxrating", _strMaxRating);
            objWriter.WriteElementString("rating", _intRating.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("ratinglabel", _strRatingLabel);
            objWriter.WriteElementString("rcgroup", _intRCGroup.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("rcdeployable", _blnDeployable.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("specialmodification", _blnSpecialModification.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("conceal", _strConceal);
            if (!string.IsNullOrEmpty(_strDicePool))
                objWriter.WriteElementString("dicepool", _strDicePool);
            objWriter.WriteElementString("avail", _strAvail);
            objWriter.WriteElementString("cost", _strCost);
            objWriter.WriteElementString("weight", _strWeight);
            objWriter.WriteElementString("included", _blnIncludedInWeapon.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("equipped", _blnEquipped.ToString(GlobalSettings.InvariantCultureInfo));
            if (_nodAllowGear != null)
                objWriter.WriteRaw(_nodAllowGear.OuterXml);
            objWriter.WriteElementString("source", _strSource);
            objWriter.WriteElementString("page", _strPage);
            objWriter.WriteElementString("accuracy", _strAccuracy);
            if (_lstGear.Count > 0)
            {
                objWriter.WriteStartElement("gears");
                foreach (Gear objGear in _lstGear)
                {
                    objGear.Save(objWriter);
                }
                objWriter.WriteEndElement();
            }
            objWriter.WriteElementString("ammoreplace", _strAmmoReplace);
            objWriter.WriteElementString("ammoslots", _intAmmoSlots.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("modifyammocapacity", _strModifyAmmoCapacity);
            objWriter.WriteElementString("damagetype", _strDamageType);
            objWriter.WriteElementString("damage", _strDamage);
            objWriter.WriteElementString("reach", _strReach);
            objWriter.WriteElementString("damagereplace", _strDamageReplace);
            objWriter.WriteElementString("firemode", _strFireMode);
            objWriter.WriteElementString("firemodereplace", _strFireModeReplace);
            objWriter.WriteElementString("ap", _strAP);
            objWriter.WriteElementString("apreplace", _strAPReplace);
            objWriter.WriteElementString("notes", _strNotes.CleanOfInvalidUnicodeChars());
            objWriter.WriteElementString("notesColor", ColorTranslator.ToHtml(_colNotes));
            objWriter.WriteElementString("discountedcost", _blnDiscountCost.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("singleshot", _intSingleShot.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("shortburst", _intShortBurst.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("longburst", _intLongBurst.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("fullburst", _intFullBurst.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("suppressive", _intSuppressive.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("replacerange", _strReplaceRange);
            objWriter.WriteElementString("rangebonus", _strRangeBonus);
            objWriter.WriteElementString("rangemodifier", _strRangeModifier);
            objWriter.WriteElementString("extra", _strExtra);
            objWriter.WriteElementString("ammobonus", _strAmmoBonus);
            objWriter.WriteElementString("wirelesson", _blnWirelessOn.ToString(GlobalSettings.InvariantCultureInfo));
            if (_nodWirelessBonus != null)
                objWriter.WriteRaw(_nodWirelessBonus.OuterXml);
            else
                objWriter.WriteElementString("wirelessbonus", string.Empty);
            if (_nodWirelessWeaponBonus != null)
                objWriter.WriteRaw(_nodWirelessWeaponBonus.OuterXml);
            else
                objWriter.WriteElementString("wirelessweaponbonus", string.Empty);
            objWriter.WriteElementString("stolen", _blnStolen.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("sortorder", _intSortOrder.ToString(GlobalSettings.InvariantCultureInfo));
            objWriter.WriteElementString("parentid", _strParentID);
            objWriter.WriteEndElement();
        }

        /// <summary>
        /// Load the CharacterAttribute from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        /// <param name="blnCopy">Whether another node is being copied.</param>
        public void Load(XmlNode objNode, bool blnCopy = false)
        {
            if (objNode == null)
                return;
            if (blnCopy || !objNode.TryGetField("guid", Guid.TryParse, out _guiID))
            {
                _guiID = Guid.NewGuid();
            }

            if (objNode.TryGetStringFieldQuickly("name", ref _strName))
            {
                _objCachedMyXmlNode = null;
                _objCachedMyXPathNode = null;
            }

            Lazy<XmlNode> objMyNode = new Lazy<XmlNode>(() => this.GetNode());
            if (!objNode.TryGetGuidFieldQuickly("sourceid", ref _guiSourceID))
            {
                objMyNode.Value?.TryGetGuidFieldQuickly("id", ref _guiSourceID);
            }
            objNode.TryGetStringFieldQuickly("mount", ref _strMount);
            objNode.TryGetStringFieldQuickly("extramount", ref _strExtraMount);
            objNode.TryGetStringFieldQuickly("addmount", ref _strAddMount);
            objNode.TryGetStringFieldQuickly("rc", ref _strRC);
            objNode.TryGetInt32FieldQuickly("rating", ref _intRating);
            objNode.TryGetStringFieldQuickly("ratinglabel", ref _strRatingLabel);
            objNode.TryGetInt32FieldQuickly("rcgroup", ref _intRCGroup);
            objNode.TryGetStringFieldQuickly("accuracy", ref _strAccuracy);
            if (_strAccuracy == "0" || _strAccuracy == "+0" || _strAccuracy == "-0")
                _strAccuracy = string.Empty;
            if (!objNode.TryGetStringFieldQuickly("maxrating", ref _strMaxRating))
            {
                // Loading older save before maxrating was tracked for Weapon Accessories
                objMyNode.Value?.TryGetStringFieldQuickly("rating", ref _strMaxRating);
            }
            // Needed for legacy reasons
            _intRating = Math.Min(_intRating, MaxRatingValue);
            objNode.TryGetStringFieldQuickly("conceal", ref _strConceal);
            objNode.TryGetBoolFieldQuickly("rcdeployable", ref _blnDeployable);
            objNode.TryGetStringFieldQuickly("avail", ref _strAvail);
            objNode.TryGetStringFieldQuickly("cost", ref _strCost);
            if (!objNode.TryGetStringFieldQuickly("weight", ref _strWeight))
                objMyNode.Value?.TryGetStringFieldQuickly("weight", ref _strWeight);
            objNode.TryGetBoolFieldQuickly("included", ref _blnIncludedInWeapon);
            objNode.TryGetBoolFieldQuickly("equipped", ref _blnEquipped);
            objNode.TryGetBoolFieldQuickly("specialmodification", ref _blnSpecialModification);
            // Compatibility sweep for older versions where some special modifications weren't flagged as such
            if (!_blnSpecialModification && _objCharacter.LastSavedVersion < new ValueVersion(5, 212, 11) && _strName.Contains("Special Modification"))
            {
                objMyNode.Value?.TryGetBoolFieldQuickly("specialmodification", ref _blnSpecialModification);
            }
            if (!_blnEquipped)
            {
                objNode.TryGetBoolFieldQuickly("installed", ref _blnEquipped);
            }
            if (!objNode.TryGetBoolFieldQuickly("wirelesson", ref _blnWirelessOn))
                _blnWirelessOn = false;
            _nodAllowGear = objNode["allowgear"];
            objNode.TryGetStringFieldQuickly("source", ref _strSource);

            objNode.TryGetStringFieldQuickly("page", ref _strPage);
            objNode.TryGetStringFieldQuickly("dicepool", ref _strDicePool);

            objNode.TryGetStringFieldQuickly("ammoreplace", ref _strAmmoReplace);
            objNode.TryGetInt32FieldQuickly("ammoslots", ref _intAmmoSlots);
            objNode.TryGetStringFieldQuickly("modifyammocapacity", ref _strModifyAmmoCapacity);

            XmlElement xmlGearsNode = objNode["gears"];
            if (xmlGearsNode != null)
            {
                using (XmlNodeList nodChildren = xmlGearsNode.SelectNodes("gear"))
                {
                    if (nodChildren != null)
                    {
                        foreach (XmlNode nodChild in nodChildren)
                        {
                            Gear objGear = new Gear(_objCharacter);
                            objGear.Load(nodChild, blnCopy);
                            _lstGear.Add(objGear);
                        }
                    }
                }
            }
            objNode.TryGetStringFieldQuickly("notes", ref _strNotes);

            string sNotesColor = ColorTranslator.ToHtml(ColorManager.HasNotesColor);
            objNode.TryGetStringFieldQuickly("notesColor", ref sNotesColor);
            _colNotes = ColorTranslator.FromHtml(sNotesColor);

            objNode.TryGetBoolFieldQuickly("discountedcost", ref _blnDiscountCost);

            objNode.TryGetStringFieldQuickly("damage", ref _strDamage);
            objNode.TryGetStringFieldQuickly("damagetype", ref _strDamageType);
            objNode.TryGetStringFieldQuickly("damagereplace", ref _strDamageReplace);
            objNode.TryGetStringFieldQuickly("firemode", ref _strFireMode);
            objNode.TryGetStringFieldQuickly("firemodereplace", ref _strFireModeReplace);
            objNode.TryGetStringFieldQuickly("ap", ref _strAP);
            objNode.TryGetStringFieldQuickly("apreplace", ref _strAPReplace);
            objNode.TryGetStringFieldQuickly("reach", ref _strReach);
            if (_strReach == "0" || _strReach == "+0" || _strReach == "-0")
                _strReach = string.Empty;
            objNode.TryGetInt32FieldQuickly("accessorycostmultiplier", ref _intAccessoryCostMultiplier);
            string strTemp = string.Empty;
            if (objNode.TryGetStringFieldQuickly("addmode", ref strTemp))
            {
                if (string.IsNullOrEmpty(_strFireMode))
                    _strFireMode = strTemp;
                else if (!_strFireMode.Contains(strTemp))
                    _strFireMode += '/' + strTemp;
            }
            objNode.TryGetInt32FieldQuickly("singleshot", ref _intSingleShot);
            objNode.TryGetInt32FieldQuickly("shortburst", ref _intShortBurst);
            objNode.TryGetInt32FieldQuickly("longburst", ref _intLongBurst);
            objNode.TryGetInt32FieldQuickly("fullburst", ref _intFullBurst);
            objNode.TryGetInt32FieldQuickly("suppressive", ref _intSuppressive);
            objNode.TryGetStringFieldQuickly("replacerange", ref _strReplaceRange);
            objNode.TryGetStringFieldQuickly("rangebonus", ref _strRangeBonus);
            objNode.TryGetStringFieldQuickly("rangemodifier", ref _strRangeModifier);
            objNode.TryGetStringFieldQuickly("extra", ref _strExtra);
            objNode.TryGetStringFieldQuickly("ammobonus", ref _strAmmoBonus);
            objNode.TryGetInt32FieldQuickly("sortorder", ref _intSortOrder);
            _nodWirelessBonus = objNode["wirelessbonus"];
            _nodWirelessWeaponBonus = objNode["wirelessweaponbonus"];
            // Legacy sweep
            if (_objCharacter.LastSavedVersion < new ValueVersion(5, 225, 933))
            {
                if (_nodWirelessBonus == null)
                    _nodWirelessBonus = objMyNode.Value?["wirelessbonus"];
                if (_nodWirelessWeaponBonus == null)
                    _nodWirelessWeaponBonus = objMyNode.Value?["wirelessweaponbonus"];
            }
            objNode.TryGetBoolFieldQuickly("stolen", ref _blnStolen);
            objNode.TryGetStringFieldQuickly("parentid", ref _strParentID);
            if (blnCopy)
            {
                if (!Equipped)
                {
                    _blnEquipped = true;
                    Equipped = false;
                }
                RefreshWirelessBonuses();
            }
        }

        /// <summary>
        /// Print the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        /// <param name="objCulture">Culture in which to print.</param>
        /// <param name="strLanguageToPrint">Language in which to print</param>
        /// <param name="token">Cancellation token to listen to.</param>
        public async Task Print(XmlWriter objWriter, CultureInfo objCulture, string strLanguageToPrint, CancellationToken token = default)
        {
            if (objWriter == null)
                return;
            // <accessory>
            XmlElementWriteHelper objBaseElement = await objWriter.StartElementAsync("accessory", token).ConfigureAwait(false);
            try
            {
                await objWriter.WriteElementStringAsync("guid", InternalId, token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("sourceid", SourceIDString, token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("name", await DisplayNameAsync(strLanguageToPrint, token).ConfigureAwait(false), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("name_english", Name, token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("mount", Mount, token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("extramount", ExtraMount, token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("addmount", AddMount, token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("rc", RC, token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("conceal", (await GetTotalConcealabilityAsync(token).ConfigureAwait(false)).ToString("+#,0;-#,0;0", objCulture), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("avail", await TotalAvailAsync(objCulture, strLanguageToPrint, token).ConfigureAwait(false), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("ratinglabel", RatingLabel, token).ConfigureAwait(false);
                string strNuyenFormat = await (await _objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).GetNuyenFormatAsync(token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("cost", (await GetTotalCostAsync(token).ConfigureAwait(false)).ToString(strNuyenFormat, objCulture), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("owncost", (await GetOwnCostAsync(token).ConfigureAwait(false)).ToString(strNuyenFormat, objCulture), token).ConfigureAwait(false);
                string strWeightFormat = await (await _objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).GetWeightFormatAsync(token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("weight", TotalWeight.ToString(strWeightFormat, objCulture), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("ownweight", OwnWeight.ToString(strWeightFormat, objCulture), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("included", IncludedInWeapon.ToString(GlobalSettings.InvariantCultureInfo), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("source", await _objCharacter.LanguageBookShortAsync(Source, strLanguageToPrint, token).ConfigureAwait(false), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("page", await DisplayPageAsync(strLanguageToPrint, token).ConfigureAwait(false), token).ConfigureAwait(false);
                await objWriter.WriteElementStringAsync("accuracy", (await GetTotalAccuracyAsync(token).ConfigureAwait(false)).ToString("+#,0;-#,0;0", objCulture), token).ConfigureAwait(false);
                if (GearChildren.Count > 0)
                {
                    // <gears>
                    XmlElementWriteHelper objGearsElement = await objWriter.StartElementAsync("gears", token).ConfigureAwait(false);
                    try
                    {
                        foreach (Gear objGear in GearChildren)
                        {
                            await objGear.Print(objWriter, objCulture, strLanguageToPrint, token).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        // </gears>
                        await objGearsElement.DisposeAsync().ConfigureAwait(false);
                    }
                }
                if (GlobalSettings.PrintNotes)
                    await objWriter.WriteElementStringAsync("notes", await GetNotesAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
            }
            finally
            {
                // </accessory>
                await objBaseElement.DisposeAsync().ConfigureAwait(false);
            }
        }

        #endregion Constructor, Create, Save, Load, and Print Methods

        #region Properties

        /// <summary>
        /// Internal identifier which will be used to identify this Weapon.
        /// </summary>
        public string InternalId => _guiID.ToString("D", GlobalSettings.InvariantCultureInfo);

        /// <summary>
        /// ID of the object that added this weapon (if any).
        /// </summary>
        public string ParentID
        {
            get => _strParentID;
            set => _strParentID = value;
        }

        /// <summary>
        /// Identifier of the object within data files.
        /// </summary>
        public Guid SourceID
        {
            get => _guiSourceID;
            set
            {
                if (_guiSourceID == value)
                    return;
                _guiSourceID = value;
                _objCachedMyXmlNode = null;
                _objCachedMyXPathNode = null;
            }
        }

        /// <summary>
        /// String-formatted identifier of the <inheritdoc cref="SourceID"/> from the data files.
        /// </summary>
        public string SourceIDString => _guiSourceID.ToString("D", GlobalSettings.InvariantCultureInfo);

        /// <summary>
        /// XmlNode for the wireless bonuses (if any) this accessory provides.
        /// </summary>
        public XmlNode WirelessBonus => _nodWirelessBonus;

        /// <summary>
        /// XmlNode for the wireless bonuses (if any) this accessory provides specifically to its parent weapon.
        /// </summary>
        public XmlNode WirelessWeaponBonus => _nodWirelessWeaponBonus;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name
        {
            get => _strName;
            set
            {
                if (Interlocked.Exchange(ref _strName, value) == value)
                    return;
                _objCachedMyXmlNode = null;
                _objCachedMyXPathNode = null;
            }
        }

        /// <summary>
        /// The accessory adds to the weapon's ammunition slots.
        /// </summary>
        public int AmmoSlots
        {
            get => _intAmmoSlots;
            set => _intAmmoSlots = value;
        }

        /// <summary>
        /// The accessory modifies the weapon's ammunition capacity.
        /// </summary>
        public string ModifyAmmoCapacity
        {
            get => _strModifyAmmoCapacity;
            set => _strModifyAmmoCapacity = value;
        }

        /// <summary>
        /// Is the accessory a Special Modification, limited by the character's Special Modifications property?
        /// </summary>
        public bool SpecialModification
        {
            get => _blnSpecialModification;
            set => _blnSpecialModification = value;
        }

        /// <summary>
        /// The accessory adds to the weapon's damage value.
        /// </summary>
        public string Damage
        {
            get => _strDamage;
            set => _strDamage = value;
        }

        /// <summary>
        /// The Accessory replaces the weapon's damage value.
        /// </summary>
        public string DamageReplacement
        {
            get => _strDamageReplace;
            set => _strDamageReplace = value;
        }

        /// <summary>
        /// The Accessory changes the Damage Type.
        /// </summary>
        public string DamageType
        {
            get => _strDamageType;
            set => _strDamageType = value;
        }

        /// <summary>
        /// The accessory adds to the weapon's Armor Penetration.
        /// </summary>
        public string AP
        {
            get => _strAP;
            set => _strAP = value;
        }

        /// <summary>
        /// Whether the Accessory only grants a Recoil Bonus while deployed.
        /// </summary>
        public bool RCDeployable => _blnDeployable;

        /// <summary>
        /// Accuracy.
        /// </summary>
        public string Accuracy => _strAccuracy;

        public int TotalAccuracy
        {
            get
            {
                string strAccuracy = Accuracy;
                if (strAccuracy.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
                {
                    string strToEvaluate;
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdAccuracy))
                    {
                        sbdAccuracy.Append(strAccuracy.TrimStartOnce('+'));
                        Func<string> funcPhysicalLimitString;
                        Weapon objParent = Parent;
                        if (objParent != null)
                        {
                            Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                            sbdAccuracy.CheapReplace(strAccuracy, "{Parent Rating}",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAccuracy.CheapReplace(strAccuracy, "Parent Rating",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAccuracy.CheapReplace(strAccuracy, "{Weapon Rating}",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAccuracy.CheapReplace(strAccuracy, "Weapon Rating",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            objParent.ProcessAttributesInXPath(sbdAccuracy, strAccuracy);
                            if (objParent.ParentVehicle != null)
                            {
                                funcPhysicalLimitString = () =>
                                {
                                    string strHandling = objParent.ParentVehicle.TotalHandling;
                                    int intSlashIndex = strHandling.IndexOf('/');
                                    if (intSlashIndex != -1)
                                        strHandling = strHandling.Substring(0, intSlashIndex);
                                    return strHandling;
                                };
                            }
                            else
                                funcPhysicalLimitString = () => _objCharacter.LimitPhysical.ToString(GlobalSettings.InvariantCultureInfo);
                        }
                        else
                        {
                            sbdAccuracy.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                            _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdAccuracy, strAccuracy);
                            funcPhysicalLimitString = () => _objCharacter.LimitPhysical.ToString(GlobalSettings.InvariantCultureInfo);
                        }
                        Lazy<int> intRating = new Lazy<int>(() => Rating);
                        sbdAccuracy.CheapReplace(strAccuracy, "{Rating}", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdAccuracy.CheapReplace(strAccuracy, "Rating", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdAccuracy.CheapReplace(strAccuracy, "Physical", funcPhysicalLimitString)
                                .CheapReplace(strAccuracy, "Missile", funcPhysicalLimitString);
                        strToEvaluate = sbdAccuracy.ToString();
                    }
                    try
                    {
                        (bool blnIsSuccess, object objProcess) = CommonFunctions.EvaluateInvariantXPath(strToEvaluate);
                        if (blnIsSuccess)
                            return ((double)objProcess).StandardRound();
                    }
                    catch (OverflowException)
                    {
                        // swallow this
                    }
                    catch (InvalidCastException)
                    {
                        // swallow this
                    }
                }
                return decValue.StandardRound();
            }
        }

        public async Task<int> GetTotalAccuracyAsync(CancellationToken token = default)
        {
            string strAccuracy = Accuracy;
            if (strAccuracy.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
            {
                string strToEvaluate;
                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdAccuracy))
                {
                    sbdAccuracy.Append(strAccuracy.TrimStartOnce('+'));
                    Func<Task<string>> funcPhysicalLimitString;
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Microsoft.VisualStudio.Threading.AsyncLazy<int> intParentRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => objParent.GetRatingAsync(token), Utils.JoinableTaskFactory);
                        await sbdAccuracy.CheapReplaceAsync(strAccuracy, "{Parent Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdAccuracy.CheapReplaceAsync(strAccuracy, "Parent Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdAccuracy.CheapReplaceAsync(strAccuracy, "{Weapon Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdAccuracy.CheapReplaceAsync(strAccuracy, "Weapon Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await objParent.ProcessAttributesInXPathAsync(sbdAccuracy, strAccuracy, token: token).ConfigureAwait(false);
                        if (objParent.ParentVehicle != null)
                        {
                            funcPhysicalLimitString = async () =>
                            {
                                string strHandling = await objParent.ParentVehicle.GetTotalHandlingAsync(token).ConfigureAwait(false);
                                int intSlashIndex = strHandling.IndexOf('/');
                                if (intSlashIndex != -1)
                                    strHandling = strHandling.Substring(0, intSlashIndex);
                                return strHandling;
                            };
                        }
                        else
                            funcPhysicalLimitString = async () =>
                                (await _objCharacter.GetLimitPhysicalAsync(token).ConfigureAwait(false)).ToString(GlobalSettings
                                    .InvariantCultureInfo);
                    }
                    else
                    {
                        sbdAccuracy.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                        await (await _objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false))
                            .ProcessAttributesInXPathAsync(sbdAccuracy, strAccuracy, token: token).ConfigureAwait(false);
                        funcPhysicalLimitString = async () =>
                                (await _objCharacter.GetLimitPhysicalAsync(token).ConfigureAwait(false)).ToString(GlobalSettings
                                    .InvariantCultureInfo);
                    }
                    Microsoft.VisualStudio.Threading.AsyncLazy<int> intRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => GetRatingAsync(token), Utils.JoinableTaskFactory);
                    await sbdAccuracy.CheapReplaceAsync(strAccuracy, "{Rating}",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    await sbdAccuracy.CheapReplaceAsync(strAccuracy, "Rating",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    await sbdAccuracy.CheapReplaceAsync(strAccuracy, "Physical", funcPhysicalLimitString, token: token).ConfigureAwait(false);
                    await sbdAccuracy.CheapReplaceAsync(strAccuracy, "Missile", funcPhysicalLimitString, token: token).ConfigureAwait(false);
                    strToEvaluate = sbdAccuracy.ToString();
                }
                try
                {
                    (bool blnIsSuccess, object objProcess) = await CommonFunctions.EvaluateInvariantXPathAsync(strToEvaluate, token).ConfigureAwait(false);
                    if (blnIsSuccess)
                        return ((double)objProcess).StandardRound();
                }
                catch (OverflowException)
                {
                    // swallow this
                }
                catch (InvalidCastException)
                {
                    // swallow this
                }
            }

            return decValue.StandardRound();
        }

        /// <summary>
        /// Accessory modifies Reach by this value.
        /// </summary>
        public string Reach => _strReach;

        public int TotalReach
        {
            get
            {
                string strReach = Reach;
                if (strReach.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
                {
                    string strToEvaluate;
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdReach))
                    {
                        if (!string.IsNullOrEmpty(strReach))
                            sbdReach.Append('(').Append(strReach.TrimStartOnce('+')).Append(')');
                        Weapon objParent = Parent;
                        if (objParent != null)
                        {
                            Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                            sbdReach.CheapReplace(strReach, "{Parent Rating}",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdReach.CheapReplace(strReach, "Parent Rating",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdReach.CheapReplace(strReach, "{Weapon Rating}",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdReach.CheapReplace(strReach, "Weapon Rating",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            objParent.ProcessAttributesInXPath(sbdReach, strReach);
                        }
                        else
                        {
                            sbdReach.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                            _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdReach, strReach);
                        }
                        Lazy<int> intRating = new Lazy<int>(() => Rating);
                        sbdReach.CheapReplace(strReach, "{Rating}", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdReach.CheapReplace(strReach, "Rating", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        strToEvaluate = sbdReach.ToString();
                    }
                    try
                    {
                        (bool blnIsSuccess, object objProcess) = CommonFunctions.EvaluateInvariantXPath(strToEvaluate);
                        if (blnIsSuccess)
                            return ((double)objProcess).StandardRound();
                    }
                    catch (OverflowException)
                    {
                        // swallow this
                    }
                    catch (InvalidCastException)
                    {
                        // swallow this
                    }
                }
                return decValue.StandardRound();
            }
        }

        public async Task<int> GetTotalReachAsync(CancellationToken token = default)
        {
            string strReach = Reach;
            if (strReach.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
            {
                string strToEvaluate;
                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdReach))
                {
                    // If the cost is determined by the Rating, evaluate the expression.
                    if (!string.IsNullOrEmpty(strReach))
                        sbdReach.Append('(').Append(strReach).Append(')');
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Microsoft.VisualStudio.Threading.AsyncLazy<int> intParentRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => objParent.GetRatingAsync(token), Utils.JoinableTaskFactory);
                        await sbdReach.CheapReplaceAsync(strReach, "{Parent Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdReach.CheapReplaceAsync(strReach, "Parent Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdReach.CheapReplaceAsync(strReach, "{Weapon Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdReach.CheapReplaceAsync(strReach, "Weapon Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await objParent.ProcessAttributesInXPathAsync(sbdReach, strReach, token: token).ConfigureAwait(false);
                    }
                    else
                    {
                        sbdReach.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                        await (await _objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false))
                            .ProcessAttributesInXPathAsync(sbdReach, strReach, token: token).ConfigureAwait(false);
                    }
                    Microsoft.VisualStudio.Threading.AsyncLazy<int> intRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => GetRatingAsync(token), Utils.JoinableTaskFactory);
                    await sbdReach.CheapReplaceAsync(strReach, "{Rating}",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    await sbdReach.CheapReplaceAsync(strReach, "Rating",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    strToEvaluate = sbdReach.ToString();
                }
                try
                {
                    (bool blnIsSuccess, object objProcess) = await CommonFunctions.EvaluateInvariantXPathAsync(strToEvaluate, token).ConfigureAwait(false);
                    if (blnIsSuccess)
                        return ((double)objProcess).StandardRound();
                }
                catch (OverflowException)
                {
                    // swallow this
                }
                catch (InvalidCastException)
                {
                    // swallow this
                }
            }
            return decValue.StandardRound();
        }

        /// <summary>
        /// Accessory replaces the AP of the parent weapon with this value.
        /// </summary>
        public string APReplacement
        {
            get => _strAPReplace;
            set => _strAPReplace = value;
        }

        /// <summary>
        /// The accessory adds a Fire Mode to the weapon.
        /// </summary>
        public string FireMode
        {
            get => _strFireMode;
            set => _strFireMode = value;
        }

        /// <summary>
        /// The accessory replaces the weapon's Fire Modes.
        /// </summary>
        public string FireModeReplacement
        {
            get => _strFireModeReplace;
            set => _strFireModeReplace = value;
        }

        /// <summary>
        /// The name of the object as it should appear on printouts (translated name only).
        /// </summary>
        public string DisplayNameShort(string strLanguage)
        {
            if (strLanguage.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                return Name;

            return this.GetNodeXPath(strLanguage)?.SelectSingleNodeAndCacheExpression("translate")?.Value ?? Name;
        }

        /// <summary>
        /// The name of the object as it should appear on printouts (translated name only).
        /// </summary>
        public async Task<string> DisplayNameShortAsync(string strLanguage, CancellationToken token = default)
        {
            if (strLanguage.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                return Name;

            XPathNavigator objNode = await this.GetNodeXPathAsync(strLanguage, token: token).ConfigureAwait(false);
            return objNode != null ? objNode.SelectSingleNodeAndCacheExpression("translate", token: token)?.Value ?? Name : Name;
        }

        /// <summary>
        /// The name of the object as it should appear on printouts in the program's current language.
        /// </summary>
        public string CurrentDisplayNameShort => DisplayNameShort(GlobalSettings.Language);

        public Task<string> GetCurrentDisplayNameShortAsync(CancellationToken token = default) =>
            DisplayNameShortAsync(GlobalSettings.Language, token);

        /// <summary>
        /// The name of the object as it should be displayed in lists. Name (Extra).
        /// </summary>
        public string DisplayName(string strLanguage)
        {
            string strReturn = DisplayNameShort(strLanguage);

            if (!string.IsNullOrEmpty(Extra))
            {
                strReturn += LanguageManager.GetString("String_Space", strLanguage) + '(' + _objCharacter.TranslateExtra(Extra, strLanguage) + ')';
            }

            return strReturn;
        }

        /// <summary>
        /// The name of the object as it should be displayed in lists. Name (Extra).
        /// </summary>
        public async Task<string> DisplayNameAsync(string strLanguage, CancellationToken token = default)
        {
            string strReturn = await DisplayNameShortAsync(strLanguage, token).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(Extra))
            {
                strReturn += await LanguageManager.GetStringAsync("String_Space", strLanguage, token: token).ConfigureAwait(false) + '(' + await _objCharacter.TranslateExtraAsync(Extra, strLanguage, token: token).ConfigureAwait(false) + ')';
            }

            return strReturn;
        }

        /// <summary>
        /// The name of the object as it should be displayed in lists in the program's current language.
        /// </summary>
        public string CurrentDisplayName => DisplayName(GlobalSettings.Language);

        public Task<string> GetCurrentDisplayNameAsync(CancellationToken token = default) =>
            DisplayNameAsync(GlobalSettings.Language, token);

        /// <summary>
        /// Mount Used.
        /// </summary>
        public string Mount
        {
            get => _strMount;
            set => _strMount = value;
        }

        /// <summary>
        /// Additional mount slot used (if any).
        /// </summary>
        public string ExtraMount
        {
            get => _strExtraMount;
            set => _strExtraMount = value;
        }

        /// <summary>
        /// Mount slot added (if any).
        /// </summary>
        public string AddMount
        {
            get => _strAddMount;
            set => _strAddMount = value;
        }

        /// <summary>
        /// Recoil.
        /// </summary>
        public string RC
        {
            get => _strRC;
            set => _strRC = value;
        }

        /// <summary>
        /// Recoil Group.
        /// </summary>
        public int RCGroup => _intRCGroup;

        /// <summary>
        /// Concealability.
        /// </summary>
        public string Concealability
        {
            get => _strConceal;
            set => _strConceal = value;
        }

        public int TotalConcealability
        {
            get
            {
                string strConceal = Concealability;
                if (strConceal.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
                {
                    string strToEvaluate;
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdConceal))
                    {
                        // If the cost is determined by the Rating, evaluate the expression.
                        sbdConceal.Append(strConceal.TrimStartOnce('+'));
                        Weapon objParent = Parent;
                        if (objParent != null)
                        {
                            Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                            sbdConceal.CheapReplace(strConceal, "{Parent Rating}",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdConceal.CheapReplace(strConceal, "Parent Rating",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdConceal.CheapReplace(strConceal, "{Weapon Rating}",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdConceal.CheapReplace(strConceal, "Weapon Rating",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            objParent.ProcessAttributesInXPath(sbdConceal, strConceal);
                        }
                        else
                        {
                            sbdConceal.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                            _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdConceal, strConceal);
                        }
                        Lazy<int> intRating = new Lazy<int>(() => Rating);
                        sbdConceal.CheapReplace(strConceal, "{Rating}", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdConceal.CheapReplace(strConceal, "Rating", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        strToEvaluate = sbdConceal.ToString();
                    }
                    try
                    {
                        (bool blnIsSuccess, object objProcess) = CommonFunctions.EvaluateInvariantXPath(strToEvaluate);
                        if (blnIsSuccess)
                            return ((double)objProcess).StandardRound();
                    }
                    catch (OverflowException)
                    {
                        // swallow this
                    }
                    catch (InvalidCastException)
                    {
                        // swallow this
                    }
                }
                return decValue.StandardRound();
            }
        }

        public async Task<int> GetTotalConcealabilityAsync(CancellationToken token = default)
        {
            string strConceal = Concealability;
            if (strConceal.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
            {
                string strToEvaluate;
                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdConceal))
                {
                    // If the cost is determined by the Rating, evaluate the expression.
                    sbdConceal.Append(strConceal.TrimStartOnce('+'));
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Microsoft.VisualStudio.Threading.AsyncLazy<int> intParentRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => objParent.GetRatingAsync(token), Utils.JoinableTaskFactory);
                        await sbdConceal.CheapReplaceAsync(strConceal, "{Parent Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdConceal.CheapReplaceAsync(strConceal, "Parent Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdConceal.CheapReplaceAsync(strConceal, "{Weapon Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdConceal.CheapReplaceAsync(strConceal, "Weapon Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await objParent.ProcessAttributesInXPathAsync(sbdConceal, strConceal, token: token).ConfigureAwait(false);
                    }
                    else
                    {
                        sbdConceal.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                        await (await _objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false))
                            .ProcessAttributesInXPathAsync(sbdConceal, strConceal, token: token).ConfigureAwait(false);
                    }
                    Microsoft.VisualStudio.Threading.AsyncLazy<int> intRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => GetRatingAsync(token), Utils.JoinableTaskFactory);
                    await sbdConceal.CheapReplaceAsync(strConceal, "{Rating}",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    await sbdConceal.CheapReplaceAsync(strConceal, "Rating",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    strToEvaluate = sbdConceal.ToString();
                }
                try
                {
                    (bool blnIsSuccess, object objProcess) = await CommonFunctions.EvaluateInvariantXPathAsync(strToEvaluate, token).ConfigureAwait(false);
                    if (blnIsSuccess)
                        return ((double) objProcess).StandardRound();
                }
                catch (OverflowException)
                {
                    // swallow this
                }
                catch (InvalidCastException)
                {
                    // swallow this
                }
            }
            return decValue.StandardRound();
        }

        /// <summary>
        /// Rating.
        /// </summary>
        public int Rating
        {
            get => Math.Min(_intRating, MaxRatingValue);
            set
            {
                value = Math.Min(value, MaxRatingValue);
                if (Interlocked.Exchange(ref _intRating, value) == value)
                    return;
                if (Parent.Equipped && Parent.ParentVehicle == null && (Weight.ContainsAny("FixedValues", "Rating") || GearChildren.Any(x => x.Equipped && x.Weight.Contains("Parent Rating"))))
                {
                    bool blnDoPropertyChange = true;
                    Weapon objWeapon = Parent;
                    for (Weapon objParent = objWeapon.Parent; objParent != null; objParent = objWeapon.Parent)
                    {
                        objWeapon = objParent;
                        if (!objWeapon.Equipped || objWeapon.ParentVehicle != null)
                        {
                            blnDoPropertyChange = false;
                            break;
                        }
                    }
                    if (blnDoPropertyChange)
                    {
                        _objCharacter.OnPropertyChanged(nameof(Character.TotalCarriedWeight));
                    }
                }
                if (GearChildren.Count > 0)
                {
                    foreach (Gear objChild in GearChildren.Where(x => x.MaxRating.Contains("Parent") || x.MinRating.Contains("Parent")))
                    {
                        // This will update a child's rating if it would become out of bounds due to its parent's rating changing
                        int intCurrentRating = objChild.Rating;
                        objChild.Rating = intCurrentRating;
                    }
                }
            }
        }

        /// <summary>
        /// Rating.
        /// </summary>
        public async Task<int> GetRatingAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return Math.Min(_intRating, await GetMaxRatingValueAsync(token).ConfigureAwait(false));
        }

        /// <summary>
        /// Rating.
        /// </summary>
        public async Task SetRatingAsync(int value, CancellationToken token = default)
        {
            value = Math.Min(value, await GetMaxRatingValueAsync(token).ConfigureAwait(false));
            if (Interlocked.Exchange(ref _intRating, value) == value)
                return;
            if (Parent.Equipped && Parent.ParentVehicle == null
                && (Weight.ContainsAny("FixedValues", "Rating")
                    || await GearChildren.AnyAsync(x => x.Equipped && x.Weight.Contains("Parent Rating"), token).ConfigureAwait(false)))
            {
                bool blnDoPropertyChange = true;
                Weapon objWeapon = Parent;
                for (Weapon objParent = objWeapon.Parent; objParent != null; objParent = objWeapon.Parent)
                {
                    objWeapon = objParent;
                    if (!objWeapon.Equipped || objWeapon.ParentVehicle != null)
                    {
                        blnDoPropertyChange = false;
                        break;
                    }
                }
                if (blnDoPropertyChange)
                {
                    await _objCharacter.OnPropertyChangedAsync(nameof(Character.TotalCarriedWeight), token).ConfigureAwait(false);
                }
            }
            if (await GearChildren.CountAsync(token).ConfigureAwait(false) > 0)
            {
                await GearChildren.ForEachAsync(async objChild =>
                {
                    if (!objChild.MaxRating.Contains("Parent") && objChild.MinRating.Contains("Parent"))
                        return;
                    // This will update a child's rating if it would become out of bounds due to its parent's rating changing
                    await objChild.SetRatingAsync(await objChild.GetRatingAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Maximum Rating of the Weapon Accessory
        /// </summary>
        public string MaxRating
        {
            get => _strMaxRating;
            set => _strMaxRating = value;
        }

        /// <summary>
        /// Maximum Rating (value form).
        /// </summary>
        public int MaxRatingValue
        {
            get
            {
                string strExpression = MaxRating;
                if (string.IsNullOrEmpty(strExpression))
                    return int.MaxValue;
                return string.IsNullOrEmpty(strExpression) ? int.MaxValue : ProcessRatingString(strExpression, _intRating);
            }
            set => MaxRating = value.ToString(GlobalSettings.InvariantCultureInfo);
        }

        /// <summary>
        /// Maximum Rating (value form).
        /// </summary>
        public Task<int> GetMaxRatingValueAsync(CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled<int>(token);
            string strExpression = MaxRating;
            return string.IsNullOrEmpty(strExpression) ? Task.FromResult(int.MaxValue) : ProcessRatingStringAsync(strExpression, _intRating, token);
        }

        /// <summary>
        /// Processes a string into an int based on logical processing.
        /// </summary>
        /// <param name="strExpression"></param>
        /// <returns></returns>
        private int ProcessRatingString(string strExpression, int intRating)
        {
            strExpression = strExpression.ProcessFixedValuesString(intRating);

            if (strExpression.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
            {
                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdValue))
                {
                    sbdValue.Append(strExpression.TrimStartOnce('+'));
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                        sbdValue.CheapReplace(strExpression, "{Parent Rating}",
                            () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdValue.CheapReplace(strExpression, "Parent Rating",
                            () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdValue.CheapReplace(strExpression, "{Weapon Rating}",
                            () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdValue.CheapReplace(strExpression, "Weapon Rating",
                            () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        objParent.ProcessAttributesInXPath(sbdValue, strExpression);
                    }
                    else
                    {
                        sbdValue.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                        _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdValue, strExpression);
                    }
                    sbdValue.Replace("{Rating}", intRating.ToString(GlobalSettings.InvariantCultureInfo));
                    sbdValue.Replace("Rating", intRating.ToString(GlobalSettings.InvariantCultureInfo));
                    // This is first converted to a decimal and rounded up since some items have a multiplier that is not a whole number, such as 2.5.
                    (bool blnIsSuccess, object objProcess)
                        = CommonFunctions.EvaluateInvariantXPath(sbdValue.ToString());
                    return blnIsSuccess ? ((double)objProcess).StandardRound() : 0;
                }
            }

            return decValue.StandardRound();
        }

        /// <summary>
        /// Processes a string into an int based on logical processing.
        /// </summary>
        /// <param name="strExpression"></param>
        /// <returns></returns>
        private async Task<int> ProcessRatingStringAsync(string strExpression, int intRating, CancellationToken token = default)
        {
            strExpression = strExpression.ProcessFixedValuesString(intRating);

            if (strExpression.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
            {
                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdValue))
                {
                    sbdValue.Append(strExpression.TrimStartOnce('+'));
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Microsoft.VisualStudio.Threading.AsyncLazy<int> intParentRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => objParent.GetRatingAsync(token), Utils.JoinableTaskFactory);
                        await sbdValue.CheapReplaceAsync(strExpression, "{Parent Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdValue.CheapReplaceAsync(strExpression, "Parent Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdValue.CheapReplaceAsync(strExpression, "{Weapon Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdValue.CheapReplaceAsync(strExpression, "Weapon Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await objParent.ProcessAttributesInXPathAsync(sbdValue, strExpression, token: token).ConfigureAwait(false);
                    }
                    else
                    {
                        sbdValue.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                        await (await _objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false))
                            .ProcessAttributesInXPathAsync(sbdValue, strExpression, token: token).ConfigureAwait(false);
                    }
                    await sbdValue.CheapReplaceAsync(strExpression, "{Rating}", () => intRating.ToString(GlobalSettings.InvariantCultureInfo), token: token).ConfigureAwait(false);
                    await sbdValue.CheapReplaceAsync(strExpression, "Rating", () => intRating.ToString(GlobalSettings.InvariantCultureInfo), token: token).ConfigureAwait(false);
                    // This is first converted to a decimal and rounded up since some items have a multiplier that is not a whole number, such as 2.5.
                    (bool blnIsSuccess, object objProcess)
                        = await CommonFunctions.EvaluateInvariantXPathAsync(sbdValue.ToString(), token).ConfigureAwait(false);
                    return blnIsSuccess ? ((double)objProcess).StandardRound() : 0;
                }
            }

            return decValue.StandardRound();
        }

        public string RatingLabel
        {
            get => _strRatingLabel;
            set => _strRatingLabel = value;
        }

        /// <summary>
        /// Avail.
        /// </summary>
        public string Avail
        {
            get => _strAvail;
            set => _strAvail = value;
        }

        /// <summary>
        /// Cost.
        /// </summary>
        public string Cost
        {
            get => _strCost;
            set => _strCost = value;
        }

        /// <summary>
        /// Weight.
        /// </summary>
        public string Weight
        {
            get => _strWeight;
            set => _strWeight = value;
        }

        /// <summary>
        /// Sourcebook.
        /// </summary>
        public string Source
        {
            get => _strSource;
            set => _strSource = value;
        }

        /// <summary>
        /// Sourcebook Page Number.
        /// </summary>
        public string Page
        {
            get => _strPage;
            set => _strPage = value;
        }

        /// <summary>
        /// Sourcebook Page Number using a given language file.
        /// Returns Page if not found or the string is empty.
        /// </summary>
        /// <param name="strLanguage">Language file keyword to use.</param>
        /// <returns></returns>
        public string DisplayPage(string strLanguage)
        {
            if (strLanguage.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                return Page;
            string s = this.GetNodeXPath(strLanguage)?.SelectSingleNodeAndCacheExpression("altpage")?.Value ?? Page;
            return !string.IsNullOrWhiteSpace(s) ? s : Page;
        }

        /// <summary>
        /// Sourcebook Page Number using a given language file.
        /// Returns Page if not found or the string is empty.
        /// </summary>
        /// <param name="strLanguage">Language file keyword to use.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns></returns>
        public async Task<string> DisplayPageAsync(string strLanguage, CancellationToken token = default)
        {
            if (strLanguage.Equals(GlobalSettings.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                return Page;
            XPathNavigator objNode = await this.GetNodeXPathAsync(strLanguage, token: token).ConfigureAwait(false);
            string strReturn = objNode?.SelectSingleNodeAndCacheExpression("altpage", token: token)?.Value ?? Page;
            return !string.IsNullOrWhiteSpace(strReturn) ? strReturn : Page;
        }

        /// <summary>
        /// Whether this Accessory is part of the base weapon configuration.
        /// </summary>
        public bool IncludedInWeapon
        {
            get => _blnIncludedInWeapon;
            set => _blnIncludedInWeapon = value;
        }

        /// <summary>
        /// Whether this Accessory is installed and contributing towards the Weapon's stats.
        /// </summary>
        public bool Equipped
        {
            get => _blnEquipped;
            set
            {
                if (_blnEquipped == value)
                    return;
                _blnEquipped = value;
                Weapon objParent = Parent;
                if (objParent.Equipped && objParent.ParentVehicle == null)
                {
                    foreach (Gear objGear in GearChildren.AsEnumerableWithSideEffects())
                    {
                        if (objGear.Equipped)
                        {
                            objGear.ChangeEquippedStatus(value, true);
                        }
                    }

                    if (_objCharacter?.IsLoading == false && (!string.IsNullOrEmpty(Weight)
                                                              || GearChildren.DeepAny(
                                                                  x => x.Children.Where(y => y.Equipped),
                                                                  x => x.Equipped && !string.IsNullOrEmpty(x.Weight))))
                        _objCharacter.OnPropertyChanged(nameof(Character.TotalCarriedWeight));
                }
                else
                {
                    foreach (Gear objGear in GearChildren.AsEnumerableWithSideEffects())
                    {
                        objGear.ChangeEquippedStatus(false);
                    }
                }
            }
        }

        /// <summary>
        /// Whether this Accessory is installed and contributing towards the Weapon's stats.
        /// </summary>
        public async Task SetEquippedAsync(bool value, CancellationToken token = default)
        {
            if (_blnEquipped == value)
                return;
            _blnEquipped = value;
            Weapon objParent = Parent;
            if (objParent.Equipped && objParent.ParentVehicle == null)
            {
                await GearChildren.ForEachWithSideEffectsAsync(async objGear =>
                {
                    if (objGear.Equipped)
                    {
                        await objGear.ChangeEquippedStatusAsync(value, true, token).ConfigureAwait(false);
                    }
                }, token).ConfigureAwait(false);

                if (_objCharacter?.IsLoading == false && (!string.IsNullOrEmpty(Weight)
                                                          || await GearChildren.DeepAnyAsync(
                                                              x => x.Children,
                                                              x => x.Equipped && !string.IsNullOrEmpty(x.Weight), token: token).ConfigureAwait(false)))
                    await _objCharacter.OnPropertyChangedAsync(nameof(Character.TotalCarriedWeight), token).ConfigureAwait(false);
            }
            else
            {
                await GearChildren.ForEachWithSideEffectsAsync(objGear =>
                    objGear.ChangeEquippedStatusAsync(false, token: token), token: token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Notes.
        /// </summary>
        public string Notes
        {
            get => _strNotes;
            set => _strNotes = value;
        }

        public Task<string> GetNotesAsync(CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled<string>(token);
            return Task.FromResult(_strNotes);
        }

        public Task SetNotesAsync(string value, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);
            _strNotes = value;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Forecolor to use for Notes in treeviews.
        /// </summary>
        public Color NotesColor
        {
            get => _colNotes;
            set => _colNotes = value;
        }

        public Task<Color> GetNotesColorAsync(CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled<Color>(token);
            return Task.FromResult(_colNotes);
        }

        public Task SetNotesColorAsync(Color value, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);
            _colNotes = value;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Total Availability in the program's current language.
        /// </summary>
        public string DisplayTotalAvail => TotalAvail(GlobalSettings.CultureInfo, GlobalSettings.Language);

        /// <summary>
        /// Total Availability in the program's current language.
        /// </summary>
        public Task<string> GetDisplayTotalAvailAsync(CancellationToken token = default) => TotalAvailAsync(GlobalSettings.CultureInfo, GlobalSettings.Language, token);

        /// <summary>
        /// Total Availability.
        /// </summary>
        public string TotalAvail(CultureInfo objCulture, string strLanguage)
        {
            return TotalAvailTuple().ToString(objCulture, strLanguage);
        }

        /// <summary>
        /// Calculated Availability of the Vehicle.
        /// </summary>
        public async Task<string> TotalAvailAsync(CultureInfo objCulture, string strLanguage, CancellationToken token = default)
        {
            return await (await TotalAvailTupleAsync(token: token).ConfigureAwait(false)).ToStringAsync(objCulture, strLanguage, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Total Availability as a triple.
        /// </summary>
        public AvailabilityValue TotalAvailTuple(bool blnCheckChildren = true)
        {
            bool blnModifyParentAvail = false;
            string strAvail = Avail;
            char chrLastAvailChar = ' ';
            int intAvail = 0;
            if (strAvail.Length > 0)
            {
                strAvail = strAvail.ProcessFixedValuesString(() => Rating);

                chrLastAvailChar = strAvail[strAvail.Length - 1];
                if (chrLastAvailChar == 'F' || chrLastAvailChar == 'R')
                {
                    strAvail = strAvail.Substring(0, strAvail.Length - 1);
                }

                blnModifyParentAvail = strAvail.StartsWith('+', '-');
                if (strAvail.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
                {
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdAvail))
                    {
                        sbdAvail.Append(strAvail.TrimStart('+'));
                        Weapon objParent = Parent;
                        if (objParent != null)
                        {
                            Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                            sbdAvail.CheapReplace(strAvail, "{Parent Rating}",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAvail.CheapReplace(strAvail, "Parent Rating",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAvail.CheapReplace(strAvail, "{Weapon Rating}",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAvail.CheapReplace(strAvail, "Weapon Rating",
                                () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            objParent.ProcessAttributesInXPath(sbdAvail, strAvail);
                        }
                        else
                        {
                            sbdAvail.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                            _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdAvail, strAvail);
                        }
                        Lazy<int> intRating = new Lazy<int>(() => Rating);
                        sbdAvail.CheapReplace(strAvail, "{Rating}", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdAvail.CheapReplace(strAvail, "Rating", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        (bool blnIsSuccess, object objProcess)
                            = CommonFunctions.EvaluateInvariantXPath(sbdAvail.ToString());
                        if (blnIsSuccess)
                            intAvail += ((double)objProcess).StandardRound();
                    }
                }
                else
                    intAvail += decValue.StandardRound();
            }

            if (blnCheckChildren)
            {
                // Run through gear children and increase the Avail by any Mod whose Avail starts with "+" or "-".
                foreach (Gear objChild in GearChildren)
                {
                    if (objChild.ParentID != InternalId)
                    {
                        AvailabilityValue objLoopAvailTuple = objChild.TotalAvailTuple();
                        if (objLoopAvailTuple.AddToParent)
                            intAvail += objLoopAvailTuple.Value;
                        if (objLoopAvailTuple.Suffix == 'F')
                            chrLastAvailChar = 'F';
                        else if (chrLastAvailChar != 'F' && objLoopAvailTuple.Suffix == 'R')
                            chrLastAvailChar = 'R';
                    }
                }
            }

            // Avail cannot go below 0. This typically happens when an item with Avail 0 is given the Second Hand category.
            if (intAvail < 0)
                intAvail = 0;

            return new AvailabilityValue(intAvail, chrLastAvailChar, blnModifyParentAvail, IncludedInWeapon);
        }

        /// <summary>
        /// Total Availability as a triple.
        /// </summary>
        public async Task<AvailabilityValue> TotalAvailTupleAsync(bool blnCheckChildren = true, CancellationToken token = default)
        {
            bool blnModifyParentAvail = false;
            string strAvail = Avail;
            char chrLastAvailChar = ' ';
            int intAvail = 0;
            if (strAvail.Length > 0)
            {
                strAvail = await strAvail.ProcessFixedValuesStringAsync(() => GetRatingAsync(token), token).ConfigureAwait(false);

                chrLastAvailChar = strAvail[strAvail.Length - 1];
                if (chrLastAvailChar == 'F' || chrLastAvailChar == 'R')
                {
                    strAvail = strAvail.Substring(0, strAvail.Length - 1);
                }

                blnModifyParentAvail = strAvail.StartsWith('+', '-');
                if (strAvail.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decValue))
                {
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdAvail))
                    {
                        sbdAvail.Append(strAvail.TrimStart('+'));
                        Weapon objParent = Parent;
                        if (objParent != null)
                        {
                            Microsoft.VisualStudio.Threading.AsyncLazy<int> intParentRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => objParent.GetRatingAsync(token), Utils.JoinableTaskFactory);
                            await sbdAvail.CheapReplaceAsync(strAvail, "{Parent Rating}",
                                async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                token: token).ConfigureAwait(false);
                            await sbdAvail.CheapReplaceAsync(strAvail, "Parent Rating",
                                async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                token: token).ConfigureAwait(false);
                            await sbdAvail.CheapReplaceAsync(strAvail, "{Weapon Rating}",
                                async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                token: token).ConfigureAwait(false);
                            await sbdAvail.CheapReplaceAsync(strAvail, "Weapon Rating",
                                async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                token: token).ConfigureAwait(false);
                            await objParent.ProcessAttributesInXPathAsync(sbdAvail, strAvail, token: token).ConfigureAwait(false);
                        }
                        else
                        {
                            sbdAvail.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                            await (await _objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false))
                                .ProcessAttributesInXPathAsync(sbdAvail, strAvail, token: token).ConfigureAwait(false);
                        }
                        Microsoft.VisualStudio.Threading.AsyncLazy<int> intRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => GetRatingAsync(token), Utils.JoinableTaskFactory);
                        await sbdAvail.CheapReplaceAsync(strAvail, "{Rating}", async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo), token: token).ConfigureAwait(false);
                        await sbdAvail.CheapReplaceAsync(strAvail, "Rating", async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo), token: token).ConfigureAwait(false);
                        (bool blnIsSuccess, object objProcess)
                            = await CommonFunctions.EvaluateInvariantXPathAsync(sbdAvail.ToString(), token).ConfigureAwait(false);
                        if (blnIsSuccess)
                            intAvail += ((double)objProcess).StandardRound();
                    }
                }
                else
                    intAvail += decValue.StandardRound();
            }

            if (blnCheckChildren)
            {
                // Run through gear children and increase the Avail by any Mod whose Avail starts with "+" or "-".
                intAvail += await GearChildren.SumAsync(async objChild =>
                {
                    if (objChild.ParentID == InternalId)
                        return 0;
                    AvailabilityValue objLoopAvailTuple
                        = await objChild.TotalAvailTupleAsync(token: token).ConfigureAwait(false);
                    if (objLoopAvailTuple.Suffix == 'F')
                        chrLastAvailChar = 'F';
                    else if (chrLastAvailChar != 'F' && objLoopAvailTuple.Suffix == 'R')
                        chrLastAvailChar = 'R';
                    return objLoopAvailTuple.AddToParent ? await objLoopAvailTuple.GetValueAsync(token).ConfigureAwait(false) : 0;
                }, token).ConfigureAwait(false);
            }

            // Avail cannot go below 0. This typically happens when an item with Avail 0 is given the Second Hand category.
            if (intAvail < 0)
                intAvail = 0;

            return new AvailabilityValue(intAvail, chrLastAvailChar, blnModifyParentAvail, IncludedInWeapon);
        }

        /// <summary>
        /// AllowGear node from the XML file.
        /// </summary>
        public XmlNode AllowGear
        {
            get => _nodAllowGear;
            set => _nodAllowGear = value;
        }

        /// <summary>
        /// A List of the Gear attached to the Cyberware.
        /// </summary>
        public TaggedObservableCollection<Gear> GearChildren => _lstGear;

        /// <summary>
        /// Whether the Armor's cost should be discounted by 10% through the Black Market Pipeline Quality.
        /// </summary>
        public bool DiscountCost
        {
            get => _blnDiscountCost;
            set => _blnDiscountCost = value;
        }

        /// <summary>
        /// Parent Weapon.
        /// </summary>
        public Weapon Parent
        {
            get => _objParent;
            set
            {
                if (Interlocked.Exchange(ref _objParent, value) == value || value == null)
                    return;
                if (value.ParentVehicle != null)
                {
                    foreach (Gear objGear in GearChildren.AsEnumerableWithSideEffects())
                    {
                        objGear.ChangeEquippedStatus(false);
                    }
                }
                else if (Equipped)
                {
                    foreach (Gear objGear in GearChildren.AsEnumerableWithSideEffects())
                    {
                        objGear.ChangeEquippedStatus(true);
                    }
                }
            }
        }

        /// <summary>
        /// Total cost of the Weapon Accessory.
        /// </summary>
        public decimal TotalCost => OwnCost + GearChildren.Sum(x => x.TotalCost);

        /// <summary>
        /// Total cost of the Weapon Accessory.
        /// </summary>
        public async Task<decimal> GetTotalCostAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return await GetOwnCostAsync(token).ConfigureAwait(false)
                   + await GearChildren.SumAsync(g => g.GetTotalCostAsync(token), token).ConfigureAwait(false);
        }

        public decimal StolenTotalCost => CalculatedStolenTotalCost(true);

        public decimal NonStolenTotalCost => CalculatedStolenTotalCost(false);

        public decimal CalculatedStolenTotalCost(bool blnStolen)
        {
            decimal decPlugin = GearChildren.Sum(g => g.CalculatedStolenTotalCost(blnStolen));
            if (Stolen != blnStolen)
                return decPlugin;

            // Add in the cost of any Gear the Weapon Accessory has attached to it.
            return OwnCost + decPlugin;
        }

        public Task<decimal> GetStolenTotalCostAsync(CancellationToken token = default) => CalculatedStolenTotalCostAsync(true, token);

        public Task<decimal> GetNonStolenTotalCostAsync(CancellationToken token = default) => CalculatedStolenTotalCostAsync(false, token);

        public async Task<decimal> CalculatedStolenTotalCostAsync(bool blnStolen, CancellationToken token = default)
        {
            decimal decPlugin = await GearChildren
                                      .SumAsync(g => g.CalculatedStolenTotalCostAsync(blnStolen, token), token)
                                      .ConfigureAwait(false);
            if (Stolen != blnStolen)
                return decPlugin;

            // Add in the cost of any Gear the Weapon Accessory has attached to it.
            return await GetOwnCostAsync(token).ConfigureAwait(false) + decPlugin;
        }

        /// <summary>
        /// The cost of just the Weapon Accessory itself.
        /// </summary>
        public decimal OwnCost
        {
            get
            {
                if (IncludedInWeapon)
                    return 0;
                string strCostExpr = Cost;
                if (string.IsNullOrEmpty(strCostExpr))
                    return 0;
                strCostExpr = strCostExpr.ProcessFixedValuesString(() => Rating);
                if (strCostExpr.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decReturn))
                {
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdCost))
                    {
                        sbdCost.Append(strCostExpr.TrimStart('+'));
                        Weapon objParent = Parent;
                        if (objParent != null)
                        {
                            Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                            sbdCost.CheapReplace(strCostExpr, "{Parent Rating}", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdCost.CheapReplace(strCostExpr, "Parent Rating", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdCost.CheapReplace(strCostExpr, "{Weapon Rating}", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdCost.CheapReplace(strCostExpr, "Weapon Rating", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            Lazy<decimal> decParentCost = new Lazy<decimal>(() => objParent.OwnCost);
                            sbdCost.CheapReplace(strCostExpr, "{Weapon Cost}", () => decParentCost.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdCost.CheapReplace(strCostExpr, "Weapon Cost", () => decParentCost.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            Lazy<decimal> decParentTotalCost = new Lazy<decimal>(() => objParent.MultipliableCost(this));
                            sbdCost.CheapReplace(strCostExpr, "{Weapon Total Cost}", () => decParentTotalCost.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdCost.CheapReplace(strCostExpr, "Weapon Total Cost", () => decParentTotalCost.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            objParent.ProcessAttributesInXPath(sbdCost, strCostExpr);
                        }
                        else
                        {
                            sbdCost.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Cost}", "0")
                                .Replace("Weapon Cost", "0")
                                .Replace("{Weapon Total Cost}", "0")
                                .Replace("Weapon Total Cost", "0");
                            _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdCost, strCostExpr);
                        }
                        Lazy<int> intRating = new Lazy<int>(() => Rating);
                        sbdCost.CheapReplace(strCostExpr, "{Rating}", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdCost.CheapReplace(strCostExpr, "Rating", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        (bool blnIsSuccess, object objProcess) = CommonFunctions.EvaluateInvariantXPath(sbdCost.ToString());
                        if (blnIsSuccess)
                            decReturn = Convert.ToDecimal((double)objProcess);
                    }
                }

                if (DiscountCost)
                    decReturn *= 0.9m;
                if (Parent != null)
                {
                    decReturn *= Parent.AccessoryMultiplier;
                    if (!string.IsNullOrEmpty(Parent.DoubledCostModificationSlots))
                    {
                        bool blnBreakAfterFound = string.IsNullOrEmpty(Mount) || string.IsNullOrEmpty(ExtraMount);
                        foreach (string strDoubledCostSlot in Parent.DoubledCostModificationSlots.SplitNoAlloc('/', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (strDoubledCostSlot == Mount || strDoubledCostSlot == ExtraMount)
                            {
                                decReturn *= 2;
                                if (blnBreakAfterFound)
                                    break;
                                else
                                    blnBreakAfterFound = true;
                            }
                        }
                    }
                }

                return decReturn;
            }
        }

        /// <summary>
        /// The cost of just the Weapon Accessory itself.
        /// </summary>
        public async Task<decimal> GetOwnCostAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (IncludedInWeapon)
                return 0;
            string strCostExpr = Cost;
            if (string.IsNullOrEmpty(strCostExpr))
                return 0;
            strCostExpr = await strCostExpr.ProcessFixedValuesStringAsync(() => GetRatingAsync(token), token).ConfigureAwait(false);

            if (strCostExpr.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decReturn))
            {
                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdCost))
                {
                    sbdCost.Append(strCostExpr.TrimStart('+'));
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Microsoft.VisualStudio.Threading.AsyncLazy<int> intParentRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => objParent.GetRatingAsync(token), Utils.JoinableTaskFactory);
                        await sbdCost.CheapReplaceAsync(strCostExpr, "{Parent Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdCost.CheapReplaceAsync(strCostExpr, "Parent Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdCost.CheapReplaceAsync(strCostExpr, "{Weapon Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdCost.CheapReplaceAsync(strCostExpr, "Weapon Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        Microsoft.VisualStudio.Threading.AsyncLazy<decimal> decParentCost = new Microsoft.VisualStudio.Threading.AsyncLazy<decimal>(() => objParent.GetOwnCostAsync(token), Utils.JoinableTaskFactory);
                        await sbdCost.CheapReplaceAsync(strCostExpr, "{Weapon Cost}",
                            async () => (await decParentCost.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdCost.CheapReplaceAsync(strCostExpr, "Weapon Cost",
                            async () => (await decParentCost.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        Microsoft.VisualStudio.Threading.AsyncLazy<decimal> decParentTotalCost = new Microsoft.VisualStudio.Threading.AsyncLazy<decimal>(() => objParent.MultipliableCostAsync(this, token), Utils.JoinableTaskFactory);
                        await sbdCost.CheapReplaceAsync(strCostExpr, "{Weapon Total Cost}",
                            async () => (await decParentTotalCost.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdCost.CheapReplaceAsync(strCostExpr, "Weapon Total Cost",
                            async () => (await decParentTotalCost.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await objParent.ProcessAttributesInXPathAsync(sbdCost, strCostExpr, token: token).ConfigureAwait(false);
                    }
                    else
                    {
                        sbdCost.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Cost}", "0")
                            .Replace("Weapon Cost", "0")
                            .Replace("{Weapon Total Cost}", "0")
                            .Replace("Weapon Total Cost", "0");
                        await (await _objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false))
                            .ProcessAttributesInXPathAsync(sbdCost, strCostExpr, token: token).ConfigureAwait(false);
                    }
                    Microsoft.VisualStudio.Threading.AsyncLazy<int> intRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => GetRatingAsync(token), Utils.JoinableTaskFactory);
                    await sbdCost.CheapReplaceAsync(strCostExpr, "{Rating}",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    await sbdCost.CheapReplaceAsync(strCostExpr, "Rating",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    (bool blnIsSuccess, object objProcess)
                        = await CommonFunctions.EvaluateInvariantXPathAsync(sbdCost.ToString(), token).ConfigureAwait(false);
                    if (blnIsSuccess)
                        decReturn = Convert.ToDecimal((double)objProcess);
                }
            }

            if (DiscountCost)
                decReturn *= 0.9m;
            if (Parent != null)
            {
                decReturn *= Parent.AccessoryMultiplier;
                if (!string.IsNullOrEmpty(Parent.DoubledCostModificationSlots))
                {
                    bool blnBreakAfterFound = string.IsNullOrEmpty(Mount) || string.IsNullOrEmpty(ExtraMount);
                    foreach (string strDoubledCostSlot in Parent.DoubledCostModificationSlots.SplitNoAlloc('/', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (strDoubledCostSlot == Mount || strDoubledCostSlot == ExtraMount)
                        {
                            decReturn *= 2;
                            if (blnBreakAfterFound)
                                break;
                            else
                                blnBreakAfterFound = true;
                        }
                    }
                }
            }

            return decReturn;
        }

        /// <summary>
        /// Total weight of the Weapon Accessory.
        /// </summary>
        public decimal TotalWeight => OwnWeight + GearChildren.Sum(x => x.Equipped, x => x.TotalWeight);

        /// <summary>
        /// The weight of just the Weapon Accessory itself.
        /// </summary>
        public decimal OwnWeight
        {
            get
            {
                if (IncludedInWeapon)
                    return 0;
                string strWeightExpression = Weight;
                if (string.IsNullOrEmpty(strWeightExpression))
                    return 0;

                decimal decReturn = 0;
                strWeightExpression = strWeightExpression.ProcessFixedValuesString(() => Rating);

                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdWeight))
                {
                    sbdWeight.Append(strWeightExpression.TrimStart('+'));
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                        sbdWeight.CheapReplace(strWeightExpression, "{Parent Rating}", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdWeight.CheapReplace(strWeightExpression, "Parent Rating", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdWeight.CheapReplace(strWeightExpression, "{Weapon Rating}", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdWeight.CheapReplace(strWeightExpression, "Weapon Rating", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        Lazy<decimal> decParentCost = new Lazy<decimal>(() => objParent.OwnWeight);
                        sbdWeight.CheapReplace(strWeightExpression, "{Weapon Weight}", () => decParentCost.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdWeight.CheapReplace(strWeightExpression, "Weapon Weight", () => decParentCost.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        Lazy<decimal> decParentTotalCost = new Lazy<decimal>(() => objParent.MultipliableWeight(this));
                        sbdWeight.CheapReplace(strWeightExpression, "{Weapon Total Weight}", () => decParentTotalCost.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdWeight.CheapReplace(strWeightExpression, "Weapon Total Weight", () => decParentTotalCost.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        objParent.ProcessAttributesInXPath(sbdWeight, strWeightExpression);
                    }
                    else
                    {
                        sbdWeight.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Weight}", "0")
                            .Replace("Weapon Weight", "0")
                            .Replace("{Weapon Total Weight}", "0")
                            .Replace("Weapon Total Weight", "0");
                        _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdWeight, strWeightExpression);
                    }
                    Lazy<int> intRating = new Lazy<int>(() => Rating);
                    sbdWeight.CheapReplace(strWeightExpression, "{Rating}", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                    sbdWeight.CheapReplace(strWeightExpression, "Rating", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                    (bool blnIsSuccess, object objProcess)
                        = CommonFunctions.EvaluateInvariantXPath(sbdWeight.ToString());
                    if (blnIsSuccess)
                        decReturn = Convert.ToDecimal((double)objProcess);
                }

                return decReturn;
            }
        }

        /// <summary>
        /// Dice Pool modifier.
        /// </summary>
        public decimal DicePool
        {
            get
            {
                string strDicePoolExpression = DicePoolString;
                if (string.IsNullOrEmpty(strDicePoolExpression))
                    return 0;
                strDicePoolExpression = strDicePoolExpression.ProcessFixedValuesString(() => Rating);
                if (strDicePoolExpression.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decReturn))
                {
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdDicePool))
                    {
                        sbdDicePool.Append(strDicePoolExpression.TrimStart('+'));
                        Weapon objParent = Parent;
                        if (objParent != null)
                        {
                            Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                            sbdDicePool.CheapReplace(strDicePoolExpression, "{Parent Rating}", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdDicePool.CheapReplace(strDicePoolExpression, "Parent Rating", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdDicePool.CheapReplace(strDicePoolExpression, "{Weapon Rating}", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdDicePool.CheapReplace(strDicePoolExpression, "Weapon Rating", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            objParent.ProcessAttributesInXPath(sbdDicePool, strDicePoolExpression);
                        }
                        else
                        {
                            sbdDicePool.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                            _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdDicePool, strDicePoolExpression);
                        }
                        Lazy<int> intRating = new Lazy<int>(() => Rating);
                        sbdDicePool.CheapReplace(strDicePoolExpression, "{Rating}", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdDicePool.CheapReplace(strDicePoolExpression, "Rating", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        (bool blnIsSuccess, object objProcess)
                            = CommonFunctions.EvaluateInvariantXPath(sbdDicePool.ToString());
                        if (blnIsSuccess)
                            decReturn = Convert.ToDecimal((double)objProcess);
                    }
                }

                return decReturn;
            }
        }

        /// <summary>
        /// Dice Pool modifier.
        /// </summary>
        public async Task<decimal> GetDicePoolAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            string strDicePoolExpression = DicePoolString;
            if (string.IsNullOrEmpty(strDicePoolExpression))
                return 0;
            strDicePoolExpression = await strDicePoolExpression.ProcessFixedValuesStringAsync(() => GetRatingAsync(token), token).ConfigureAwait(false);
            if (strDicePoolExpression.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decReturn))
            {
                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdDicePool))
                {
                    sbdDicePool.Append(strDicePoolExpression.TrimStart('+'));
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Microsoft.VisualStudio.Threading.AsyncLazy<int> intParentRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => objParent.GetRatingAsync(token), Utils.JoinableTaskFactory);
                        await sbdDicePool.CheapReplaceAsync(strDicePoolExpression, "{Parent Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdDicePool.CheapReplaceAsync(strDicePoolExpression, "Parent Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdDicePool.CheapReplaceAsync(strDicePoolExpression, "{Weapon Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdDicePool.CheapReplaceAsync(strDicePoolExpression, "Weapon Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await objParent.ProcessAttributesInXPathAsync(sbdDicePool, strDicePoolExpression, token: token).ConfigureAwait(false);
                    }
                    else
                    {
                        sbdDicePool.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                        await (await _objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false))
                            .ProcessAttributesInXPathAsync(sbdDicePool, strDicePoolExpression, token: token).ConfigureAwait(false);
                    }
                    Microsoft.VisualStudio.Threading.AsyncLazy<int> intRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => GetRatingAsync(token), Utils.JoinableTaskFactory);
                    await sbdDicePool.CheapReplaceAsync(strDicePoolExpression, "{Rating}",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    await sbdDicePool.CheapReplaceAsync(strDicePoolExpression, "Rating",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    (bool blnIsSuccess, object objProcess)
                        = await CommonFunctions.EvaluateInvariantXPathAsync(sbdDicePool.ToString(), token).ConfigureAwait(false);
                    if (blnIsSuccess)
                        decReturn = Convert.ToDecimal((double)objProcess);
                }
            }

            return decReturn;
        }

        private string DicePoolString => _strDicePool;

        /// <summary>
        /// Adjust the Weapon's Ammo amount by the specified percent.
        /// </summary>
        public string AmmoBonus
        {
            get => _strAmmoBonus;
            set => _strAmmoBonus = value;
        }

        public decimal TotalAmmoBonus
        {
            get
            {
                string strAmmoBonus = AmmoBonus;
                if (strAmmoBonus.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decBonus))
                {
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdAmmoBonus))
                    {
                        sbdAmmoBonus.Append(strAmmoBonus.TrimStart('+'));
                        Weapon objParent = Parent;
                        if (objParent != null)
                        {
                            Lazy<int> intParentRating = new Lazy<int>(() => objParent.Rating);
                            sbdAmmoBonus.CheapReplace(strAmmoBonus, "{Parent Rating}", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAmmoBonus.CheapReplace(strAmmoBonus, "Parent Rating", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAmmoBonus.CheapReplace(strAmmoBonus, "{Weapon Rating}", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            sbdAmmoBonus.CheapReplace(strAmmoBonus, "Weapon Rating", () => intParentRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                            objParent.ProcessAttributesInXPath(sbdAmmoBonus, strAmmoBonus);
                        }
                        else
                        {
                            sbdAmmoBonus.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                                .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                            _objCharacter.AttributeSection.ProcessAttributesInXPath(sbdAmmoBonus, strAmmoBonus);
                        }
                        Lazy<int> intRating = new Lazy<int>(() => Rating);
                        sbdAmmoBonus.CheapReplace(strAmmoBonus, "{Rating}", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        sbdAmmoBonus.CheapReplace(strAmmoBonus, "Rating", () => intRating.Value.ToString(GlobalSettings.InvariantCultureInfo));
                        (bool blnIsSuccess, object objProcess)
                            = CommonFunctions.EvaluateInvariantXPath(sbdAmmoBonus.ToString());
                        if (blnIsSuccess)
                            decBonus = Convert.ToDecimal((double)objProcess);
                    }
                }
                return decBonus;
            }
        }

        public async Task<decimal> GetTotalAmmoBonusAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            string strAmmoBonus = AmmoBonus;
            if (strAmmoBonus.DoesNeedXPathProcessingToBeConvertedToNumber(out decimal decBonus))
            {
                using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdAmmoBonus))
                {
                    sbdAmmoBonus.Append(strAmmoBonus.TrimStart('+'));
                    Weapon objParent = Parent;
                    if (objParent != null)
                    {
                        Microsoft.VisualStudio.Threading.AsyncLazy<int> intParentRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => objParent.GetRatingAsync(token), Utils.JoinableTaskFactory);
                        await sbdAmmoBonus.CheapReplaceAsync(strAmmoBonus, "{Parent Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdAmmoBonus.CheapReplaceAsync(strAmmoBonus, "Parent Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdAmmoBonus.CheapReplaceAsync(strAmmoBonus, "{Weapon Rating}",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await sbdAmmoBonus.CheapReplaceAsync(strAmmoBonus, "Weapon Rating",
                            async () => (await intParentRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                            token: token).ConfigureAwait(false);
                        await objParent.ProcessAttributesInXPathAsync(sbdAmmoBonus, strAmmoBonus, token: token).ConfigureAwait(false);
                    }
                    else
                    {
                        sbdAmmoBonus.Replace("{Parent Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Parent Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("{Weapon Rating}", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo))
                            .Replace("Weapon Rating", int.MaxValue.ToString(GlobalSettings.InvariantCultureInfo));
                        await (await _objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false))
                            .ProcessAttributesInXPathAsync(sbdAmmoBonus, strAmmoBonus, token: token).ConfigureAwait(false);
                    }
                    Microsoft.VisualStudio.Threading.AsyncLazy<int> intRating = new Microsoft.VisualStudio.Threading.AsyncLazy<int>(() => GetRatingAsync(token), Utils.JoinableTaskFactory);
                    await sbdAmmoBonus.CheapReplaceAsync(strAmmoBonus, "{Rating}",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    await sbdAmmoBonus.CheapReplaceAsync(strAmmoBonus, "Rating",
                                                    async () => (await intRating.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo),
                                                    token: token).ConfigureAwait(false);
                    (bool blnIsSuccess, object objProcess)
                        = await CommonFunctions.EvaluateInvariantXPathAsync(sbdAmmoBonus.ToString(), token).ConfigureAwait(false);
                    if (blnIsSuccess)
                        decBonus = Convert.ToDecimal((double)objProcess);
                }
            }
            return decBonus;
        }

        /// <summary>
        /// Replace the Weapon's Ammo value with the Weapon Mod's value.
        /// </summary>
        public string AmmoReplace
        {
            get => _strAmmoReplace;
            set => _strAmmoReplace = value;
        }

        /// <summary>
        /// Multiply the cost of other installed Accessories.
        /// </summary>
        public int AccessoryCostMultiplier
        {
            get => _intAccessoryCostMultiplier;
            set => _intAccessoryCostMultiplier = value;
        }

        /// <summary>
        /// Number of rounds consumed by Single Shot.
        /// </summary>
        public int SingleShot => _intFullBurst;

        /// <summary>
        /// Number of rounds consumed by Short Burst.
        /// </summary>
        public int ShortBurst => _intShortBurst;

        /// <summary>
        /// Number of rounds consumed by Long Burst.
        /// </summary>
        public int LongBurst => _intLongBurst;

        /// <summary>
        /// Number of rounds consumed by Full Burst.
        /// </summary>
        public int FullBurst => _intFullBurst;

        /// <summary>
        /// Number of rounds consumed by Suppressive Fire.
        /// </summary>
        public int Suppressive => _intSuppressive;

        /// <summary>
        /// If not empty, overrides the parent weapon's range type with this entry.
        /// </summary>
        public string ReplaceRange => _strReplaceRange;

        /// <summary>
        /// Range bonus granted by the Accessory.
        /// </summary>
        public string RangeBonus => _strRangeBonus;

        /// <summary>
        /// Range Dicepool modifier granted by the Accessory.
        /// </summary>
        public string RangeModifier => _strRangeModifier;

        /// <summary>
        /// Value that was selected during an ImprovementManager dialogue.
        /// </summary>
        public string Extra
        {
            get => _strExtra;
            set => _strExtra = value;
        }

        /// <summary>
        /// Used by our sorting algorithm to remember which order the user moves things to
        /// </summary>
        public int SortOrder
        {
            get => _intSortOrder;
            set => _intSortOrder = value;
        }

        private XmlNode _objCachedMyXmlNode;
        private string _strCachedXmlNodeLanguage = string.Empty;

        public async Task<XmlNode> GetNodeCoreAsync(bool blnSync, string strLanguage, CancellationToken token = default)
        {
            XmlNode objReturn = _objCachedMyXmlNode;
            if (objReturn != null && strLanguage == _strCachedXmlNodeLanguage
                                  && !GlobalSettings.LiveCustomData)
                return objReturn;
            XmlDocument objDoc = blnSync
                // ReSharper disable once MethodHasAsyncOverload
                ? _objCharacter.LoadData("weapons.xml", strLanguage, token: token)
                : await _objCharacter.LoadDataAsync("weapons.xml", strLanguage, token: token).ConfigureAwait(false);
            if (SourceID != Guid.Empty)
                objReturn = objDoc.TryGetNodeById("/chummer/accessories/accessory", SourceID);
            if (objReturn == null)
            {
                objReturn = objDoc.TryGetNodeByNameOrId("/chummer/accessories/accessory", Name);
                objReturn?.TryGetGuidFieldQuickly("id", ref _guiSourceID);
            }
            _objCachedMyXmlNode = objReturn;
            _strCachedXmlNodeLanguage = strLanguage;
            return objReturn;
        }

        private XPathNavigator _objCachedMyXPathNode;
        private string _strCachedXPathNodeLanguage = string.Empty;

        public async Task<XPathNavigator> GetNodeXPathCoreAsync(bool blnSync, string strLanguage, CancellationToken token = default)
        {
            XPathNavigator objReturn = _objCachedMyXPathNode;
            if (objReturn != null && strLanguage == _strCachedXPathNodeLanguage
                                  && !GlobalSettings.LiveCustomData)
                return objReturn;
            XPathNavigator objDoc = blnSync
                // ReSharper disable once MethodHasAsyncOverload
                ? _objCharacter.LoadDataXPath("weapons.xml", strLanguage, token: token)
                : await _objCharacter.LoadDataXPathAsync("weapons.xml", strLanguage, token: token).ConfigureAwait(false);
            if (SourceID != Guid.Empty)
                objReturn = objDoc.TryGetNodeById("/chummer/accessories/accessory", SourceID);
            if (objReturn == null)
            {
                objReturn = objDoc.TryGetNodeByNameOrId("/chummer/accessories/accessory", Name);
                objReturn?.TryGetGuidFieldQuickly("id", ref _guiSourceID);
            }
            _objCachedMyXPathNode = objReturn;
            _strCachedXPathNodeLanguage = strLanguage;
            return objReturn;
        }

        /// <summary>
        /// Whether this Accessory's wireless bonus is enabled
        /// </summary>
        public bool WirelessOn
        {
            get => _blnWirelessOn;
            set
            {
                if (_blnWirelessOn == value)
                    return;
                _blnWirelessOn = value;
                RefreshWirelessBonuses();
            }
        }

        /// <summary>
        /// Is the object stolen via the Stolen Gear quality?
        /// </summary>
        public bool Stolen
        {
            get => _blnStolen;
            set => _blnStolen = value;
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Toggle the Wireless Bonus for this weapon accessory.
        /// </summary>
        public void RefreshWirelessBonuses()
        {
            if (!string.IsNullOrEmpty(WirelessBonus?.InnerText))
            {
                if (WirelessOn && Equipped && Parent.WirelessOn)
                {
                    if (WirelessBonus.SelectSingleNodeAndCacheExpressionAsNavigator("@mode")?.Value == "replace")
                    {
                        ImprovementManager.DisableImprovements(_objCharacter,
                            _objCharacter.Improvements.Where(x =>
                                x.ImproveSource == Improvement.ImprovementSource.WeaponAccessory &&
                                x.SourceName == InternalId));
                    }

                    ImprovementManager.CreateImprovements(_objCharacter, Improvement.ImprovementSource.WeaponAccessory, InternalId + "Wireless", WirelessBonus, Rating, CurrentDisplayNameShort);

                    string strSelectedValue = ImprovementManager.GetSelectedValue(_objCharacter);
                    if (!string.IsNullOrEmpty(strSelectedValue) && string.IsNullOrEmpty(_strExtra))
                        _strExtra = strSelectedValue;
                }
                else
                {
                    if (WirelessBonus.SelectSingleNodeAndCacheExpressionAsNavigator("@mode")?.Value == "replace")
                    {
                        ImprovementManager.EnableImprovements(_objCharacter,
                            _objCharacter.Improvements.Where(x =>
                                x.ImproveSource == Improvement.ImprovementSource.WeaponAccessory &&
                                x.SourceName == InternalId));
                    }

                    string strSourceNameToRemove = InternalId + "Wireless";
                    ImprovementManager.RemoveImprovements(_objCharacter,
                        _objCharacter.Improvements.Where(x =>
                            x.ImproveSource == Improvement.ImprovementSource.WeaponAccessory &&
                            x.SourceName == strSourceNameToRemove).ToList());
                }
            }

            foreach (Gear objGear in GearChildren.AsEnumerableWithSideEffects())
                objGear.RefreshWirelessBonuses();
        }

        /// <summary>
        /// Toggle the Wireless Bonus for this weapon accessory.
        /// </summary>
        public async Task RefreshWirelessBonusesAsync(CancellationToken token = default)
        {
            if (!string.IsNullOrEmpty(WirelessBonus?.InnerText))
            {
                if (WirelessOn && Equipped && Parent.WirelessOn)
                {
                    if (WirelessBonus.SelectSingleNodeAndCacheExpressionAsNavigator("@mode", token)?.Value == "replace")
                    {
                        await ImprovementManager.DisableImprovementsAsync(_objCharacter,
                                                                          await _objCharacter.Improvements.ToListAsync(x =>
                                                                              x.ImproveSource == Improvement.ImprovementSource.WeaponAccessory &&
                                                                              x.SourceName == InternalId, token: token).ConfigureAwait(false), token).ConfigureAwait(false);
                    }

                    await ImprovementManager.CreateImprovementsAsync(_objCharacter,
                                                                     Improvement.ImprovementSource.WeaponAccessory,
                                                                     InternalId + "Wireless", WirelessBonus, await GetRatingAsync(token).ConfigureAwait(false),
                                                                     await GetCurrentDisplayNameShortAsync(token).ConfigureAwait(false),
                                                                     token: token).ConfigureAwait(false);

                    string strSelectedValue = ImprovementManager.GetSelectedValue(_objCharacter);
                    if (!string.IsNullOrEmpty(strSelectedValue) && string.IsNullOrEmpty(_strExtra))
                        _strExtra = strSelectedValue;
                }
                else
                {
                    if (WirelessBonus.SelectSingleNodeAndCacheExpressionAsNavigator("@mode", token)?.Value == "replace")
                    {
                        await ImprovementManager.EnableImprovementsAsync(_objCharacter,
                                                                         await _objCharacter.Improvements.ToListAsync(x =>
                                                                             x.ImproveSource == Improvement.ImprovementSource.WeaponAccessory &&
                                                                             x.SourceName == InternalId, token: token).ConfigureAwait(false), token).ConfigureAwait(false);
                    }

                    string strSourceNameToRemove = InternalId + "Wireless";
                    await ImprovementManager.RemoveImprovementsAsync(_objCharacter,
                                                                     await _objCharacter.Improvements.ToListAsync(
                                                                         x => x.ImproveSource
                                                                              == Improvement.ImprovementSource
                                                                                  .WeaponAccessory
                                                                              && x.SourceName == strSourceNameToRemove,
                                                                         token: token).ConfigureAwait(false), token: token).ConfigureAwait(false);
                }
            }

            await GearChildren.ForEachWithSideEffectsAsync(x => x.RefreshWirelessBonusesAsync(token), token: token).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks a nominated piece of gear for Availability requirements.
        /// </summary>
        /// <param name="dicRestrictedGearLimits">Dictionary of Restricted Gear availabilities still available with the amount of items that can still use that availability.</param>
        /// <param name="sbdAvailItems">StringBuilder used to list names of gear that are currently over the availability limit.</param>
        /// <param name="sbdRestrictedItems">StringBuilder used to list names of gear that are being used for Restricted Gear.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        public async Task<int> CheckRestrictedGear(IDictionary<int, int> dicRestrictedGearLimits, StringBuilder sbdAvailItems, StringBuilder sbdRestrictedItems, CancellationToken token = default)
        {
            int intRestrictedCount = 0;
            if (!IncludedInWeapon)
            {
                AvailabilityValue objTotalAvail = await TotalAvailTupleAsync(token: token).ConfigureAwait(false);
                if (!objTotalAvail.AddToParent)
                {
                    int intAvailInt = await objTotalAvail.GetValueAsync(token).ConfigureAwait(false);
                    if (intAvailInt > await _objCharacter.Settings.GetMaximumAvailabilityAsync(token).ConfigureAwait(false))
                    {
                        int intLowestValidRestrictedGearAvail = -1;
                        foreach (int intValidAvail in dicRestrictedGearLimits.Keys)
                        {
                            if (intValidAvail >= intAvailInt && (intLowestValidRestrictedGearAvail < 0
                                                                 || intValidAvail < intLowestValidRestrictedGearAvail))
                                intLowestValidRestrictedGearAvail = intValidAvail;
                        }

                        string strNameToUse = await GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
                        if (Parent != null)
                            strNameToUse += await LanguageManager.GetStringAsync("String_Space", token: token).ConfigureAwait(false) + '(' + await Parent.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + ')';

                        if (intLowestValidRestrictedGearAvail >= 0
                            && dicRestrictedGearLimits[intLowestValidRestrictedGearAvail] > 0)
                        {
                            --dicRestrictedGearLimits[intLowestValidRestrictedGearAvail];
                            sbdRestrictedItems.AppendLine().Append("\t\t").Append(strNameToUse);
                        }
                        else
                        {
                            dicRestrictedGearLimits.Remove(intLowestValidRestrictedGearAvail);
                            ++intRestrictedCount;
                            sbdAvailItems.AppendLine().Append("\t\t").Append(strNameToUse);
                        }
                    }
                }
            }

            intRestrictedCount += await GearChildren
                                        .SumAsync(objChild =>
                                                objChild
                                                    .CheckRestrictedGear(
                                                        dicRestrictedGearLimits, sbdAvailItems, sbdRestrictedItems,
                                                        token), token: token)
                                        .ConfigureAwait(false);

            return intRestrictedCount;
        }

        public decimal DeleteWeaponAccessory(bool blnDoRemoval = true)
        {
            if (blnDoRemoval)
                Parent.WeaponAccessories.Remove(this);
            // Remove any children the Gear may have.
            decimal decReturn = GearChildren.AsEnumerableWithSideEffects().Sum(x => x.DeleteGear(false));

            DisposeSelf();

            return decReturn;
        }

        public async Task<decimal> DeleteWeaponAccessoryAsync(bool blnDoRemoval = true, CancellationToken token = default)
        {
            if (blnDoRemoval)
                await Parent.WeaponAccessories.RemoveAsync(this, token).ConfigureAwait(false);
            // Remove any children the Gear may have.
            decimal decReturn = await GearChildren.SumWithSideEffectsAsync(x => x.DeleteGearAsync(false, token), token).ConfigureAwait(false);

            await DisposeSelfAsync().ConfigureAwait(false);

            return decReturn;
        }

        #region UI Methods

        public async Task<TreeNode> CreateTreeNode(ContextMenuStrip cmsWeaponAccessory, ContextMenuStrip cmsWeaponAccessoryGear, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (IncludedInWeapon && !string.IsNullOrEmpty(Source) && !await _objCharacter.Settings.BookEnabledAsync(Source, token).ConfigureAwait(false))
                return null;

            TreeNode objNode = new TreeNode
            {
                Name = InternalId,
                Text = await GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                Tag = this,
                ContextMenuStrip = cmsWeaponAccessory,
                ForeColor = PreferredColor,
                ToolTipText = Notes.WordWrap()
            };

            TreeNodeCollection lstChildNodes = objNode.Nodes;
            await GearChildren.ForEachAsync(async objGear =>
            {
                TreeNode objLoopNode = await objGear.CreateTreeNode(cmsWeaponAccessoryGear, null, token).ConfigureAwait(false);
                if (objLoopNode != null)
                {
                    lstChildNodes.Add(objLoopNode);
                    objNode.Expand();
                }
            }, token).ConfigureAwait(false);

            return objNode;
        }

        public Color PreferredColor
        {
            get
            {
                if (!string.IsNullOrEmpty(Notes))
                {
                    return IncludedInWeapon || !string.IsNullOrEmpty(ParentID)
                        ? ColorManager.GenerateCurrentModeDimmedColor(NotesColor)
                        : ColorManager.GenerateCurrentModeColor(NotesColor);
                }
                return IncludedInWeapon || !string.IsNullOrEmpty(ParentID)
                    ? ColorManager.GrayText
                    : ColorManager.WindowText;
            }
        }

        public async Task<Color> GetPreferredColorAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(await GetNotesAsync(token).ConfigureAwait(false)))
            {
                return IncludedInWeapon || !string.IsNullOrEmpty(ParentID)
                    ? ColorManager.GenerateCurrentModeDimmedColor(await GetNotesColorAsync(token).ConfigureAwait(false))
                    : ColorManager.GenerateCurrentModeColor(await GetNotesColorAsync(token).ConfigureAwait(false));
            }
            return IncludedInWeapon || !string.IsNullOrEmpty(ParentID)
                    ? ColorManager.GrayText
                    : ColorManager.WindowText;
        }

        #endregion UI Methods

        #endregion Methods

        public bool Remove(bool blnConfirmDelete = true)
        {
            if (IncludedInWeapon)
                return false;
            if (blnConfirmDelete && !CommonFunctions.ConfirmDelete(LanguageManager.GetString("Message_DeleteWeaponAccessory")))
                return false;
            DeleteWeaponAccessory();
            return true;
        }

        public async Task<bool> RemoveAsync(bool blnConfirmDelete = true, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (IncludedInWeapon)
                return false;
            if (blnConfirmDelete && !await CommonFunctions
                    .ConfirmDeleteAsync(
                        await LanguageManager.GetStringAsync("Message_DeleteWeaponAccessory", token: token)
                            .ConfigureAwait(false), token).ConfigureAwait(false))
                return false;
            await DeleteWeaponAccessoryAsync(token: token).ConfigureAwait(false);
            return true;
        }

        public bool Sell(decimal decPercentage, bool blnConfirmDelete)
        {
            if (!_objCharacter.Created)
                return Remove(blnConfirmDelete);
            if (IncludedInWeapon)
                return false;
            if (blnConfirmDelete && !CommonFunctions.ConfirmDelete(LanguageManager.GetString("Message_DeleteWeaponAccessory")))
                return false;

            // Create the Expense Log Entry for the sale.
            Weapon objParent = Parent;
            decimal decAmount;
            if (objParent != null)
            {
                decimal decOriginal = objParent.TotalCost;
                decAmount = DeleteWeaponAccessory() * decPercentage;
                decAmount += (decOriginal - objParent.TotalCost) * decPercentage;
            }
            else
            {
                decimal decOriginal = TotalCost;
                decAmount = (DeleteWeaponAccessory() + decOriginal) * decPercentage;
            }
            ExpenseLogEntry objExpense = new ExpenseLogEntry(_objCharacter);
            objExpense.Create(decAmount, LanguageManager.GetString("String_ExpenseSoldWeaponAccessory") + ' ' + CurrentDisplayNameShort, ExpenseType.Nuyen, DateTime.Now);
            _objCharacter.ExpenseEntries.AddWithSort(objExpense);
            _objCharacter.Nuyen += decAmount;
            return true;
        }

        public async Task<bool> SellAsync(decimal decPercentage, bool blnConfirmDelete,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!await _objCharacter.GetCreatedAsync(token).ConfigureAwait(false))
                return await RemoveAsync(blnConfirmDelete, token).ConfigureAwait(false);

            if (blnConfirmDelete && !await CommonFunctions
                    .ConfirmDeleteAsync(
                        await LanguageManager.GetStringAsync("Message_DeleteWeaponAccessory", token: token).ConfigureAwait(false),
                        token).ConfigureAwait(false))
                return false;

            Weapon objParent = Parent;
            decimal decAmount;
            if (objParent != null)
            {
                decimal decOriginal = await objParent.GetTotalCostAsync(token).ConfigureAwait(false);
                decAmount = await DeleteWeaponAccessoryAsync(token: token).ConfigureAwait(false) * decPercentage;
                decAmount += (decOriginal - await objParent.GetTotalCostAsync(token).ConfigureAwait(false)) * decPercentage;
            }
            else
            {
                decimal decOriginal = await GetTotalCostAsync(token).ConfigureAwait(false);
                decAmount = (await DeleteWeaponAccessoryAsync(token: token).ConfigureAwait(false) + decOriginal) * decPercentage;
            }

            // Create the Expense Log Entry for the sale.
            ExpenseLogEntry objExpense = new ExpenseLogEntry(_objCharacter);
            objExpense.Create(decAmount,
                await LanguageManager.GetStringAsync("String_ExpenseSoldWeaponAccessory", token: token).ConfigureAwait(false) +
                ' ' + await GetCurrentDisplayNameShortAsync(token).ConfigureAwait(false), ExpenseType.Nuyen,
                DateTime.Now);
            await _objCharacter.ExpenseEntries.AddWithSortAsync(objExpense, token: token).ConfigureAwait(false);
            await _objCharacter.ModifyNuyenAsync(decAmount, token).ConfigureAwait(false);
            return true;
        }

        public void SetSourceDetail(Control sourceControl)
        {
            if (_objCachedSourceDetail.Language != GlobalSettings.Language)
                _objCachedSourceDetail = default;
            SourceDetail.SetControl(sourceControl);
        }

        public async Task SetSourceDetailAsync(Control sourceControl, CancellationToken token = default)
        {
            if (_objCachedSourceDetail.Language != GlobalSettings.Language)
                _objCachedSourceDetail = default;
            await (await GetSourceDetailAsync(token).ConfigureAwait(false)).SetControlAsync(sourceControl, token).ConfigureAwait(false);
        }

        public async Task<bool> AllowPasteXml(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            IAsyncDisposable objLocker = await GlobalSettings.EnterClipboardReadLockAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                switch (await GlobalSettings.GetClipboardContentTypeAsync(token).ConfigureAwait(false))
                {
                    case ClipboardContentType.Gear:
                        XPathNavigator checkNode =
                            (await GlobalSettings.GetClipboardAsync(token).ConfigureAwait(false)).SelectSingleNodeAndCacheExpressionAsNavigator(
                                "/character/gears/gear", token);
                        if (checkNode == null)
                            return false;
                        string strCheckValue = checkNode.SelectSingleNodeAndCacheExpression("category", token)?.Value;
                        if (!string.IsNullOrEmpty(strCheckValue))
                        {
                            XmlNodeList xmlGearCategoryList = AllowGear?.SelectNodes("gearcategory");
                            if (xmlGearCategoryList?.Count > 0 && xmlGearCategoryList.Cast<XmlNode>()
                                    .Any(objAllowed => objAllowed.InnerText == strCheckValue))
                            {
                                return true;
                            }
                        }

                        strCheckValue = checkNode.SelectSingleNodeAndCacheExpression("name", token)?.Value;
                        if (!string.IsNullOrEmpty(strCheckValue))
                        {
                            XmlNodeList xmlGearNameList = AllowGear?.SelectNodes("gearname");
                            if (xmlGearNameList?.Count > 0 && xmlGearNameList.Cast<XmlNode>()
                                    .Any(objAllowed => objAllowed.InnerText == strCheckValue))
                            {
                                return true;
                            }
                        }

                        return false;

                    default:
                        return false;
                }
            }
            finally
            {
                await objLocker.DisposeAsync().ConfigureAwait(false);
            }
        }

        public Task<bool> AllowPasteObject(object input, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _lstGear.EnumerateWithSideEffects().ForEach(x => x.Dispose());
            DisposeSelf();
        }

        private void DisposeSelf()
        {
            _lstGear.Dispose();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await _lstGear.ForEachWithSideEffectsAsync(async x => await x.DisposeAsync().ConfigureAwait(false)).ConfigureAwait(false);
            await DisposeSelfAsync().ConfigureAwait(false);
        }

        private ValueTask DisposeSelfAsync()
        {
            return _lstGear.DisposeAsync();
        }

        public Character CharacterObject => _objCharacter;
    }
}

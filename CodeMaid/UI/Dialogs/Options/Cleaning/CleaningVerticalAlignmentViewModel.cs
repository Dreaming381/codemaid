using SteveCadwallader.CodeMaid.Properties;

namespace SteveCadwallader.CodeMaid.UI.Dialogs.Options.Cleaning
{
    /// <summary>
    /// The view model for cleaning insert options.
    /// </summary>
    public class CleaningVerticalAlignmentViewModel : OptionsPageViewModel
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CleaningVerticalAlignmentViewModel" /> class.
        /// </summary>
        /// <param name="package">The hosting package.</param>
        /// <param name="activeSettings">The active settings.</param>
        public CleaningVerticalAlignmentViewModel(CodeMaidPackage package, Settings activeSettings)
            : base(package, activeSettings)
        {
            Mappings = new SettingsToOptionsList(ActiveSettings, this)
            {
                new SettingToOptionMapping<bool, bool>(x => ActiveSettings.Cleaning_VerticallyAlignAfterAssignements, x => VerticallyAlignAfterAssignments),
                new SettingToOptionMapping<bool, bool>(x => ActiveSettings.Cleaning_VerticallyAlignAfterTypesAndModifiers, x => VerticallyAlignAfterTypesAndModifiers)
            };
        }

        #endregion Constructors

        #region Overrides of OptionsPageViewModel

        /// <summary>
        /// Gets the header.
        /// </summary>
        public override string Header => Resources.VerticallyAlign;

        #endregion Overrides of OptionsPageViewModel

        #region Options

        /// <summary>
        /// Gets or sets the flag indicating if member names after types and modifiers should be vertically aligned.
        /// </summary>
        public bool VerticallyAlignAfterAssignments
        {
            get { return GetPropertyValue<bool>(); }
            set { SetPropertyValue(value); }
        }

        /// <summary>
        /// Gets or sets the flag indicating if assignment operators should be vertically aligned.
        /// </summary>
        public bool VerticallyAlignAfterTypesAndModifiers
        {
            get { return GetPropertyValue<bool>(); }
            set { SetPropertyValue(value); }
        }

        #endregion Options
    }
}
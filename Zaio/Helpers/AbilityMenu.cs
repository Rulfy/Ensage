using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ensage.Common.Enums;
using Ensage.Common.Menu;

namespace Zaio
{
    public class AbilityMenu
    {
        private MenuItem _abilityMenu;
        private List<AbilityId> _abilityIds;

        private MenuItem _killstealAbilityMenu;
        private List<AbilityId> _killstealAbilityIds;
        public AbilityMenu(List<AbilityId> abilites, MenuItem abilityMenu, List<AbilityId> killstealAbilities,
            MenuItem killstealAbilityMenu)
        {
            _abilityMenu = abilityMenu;
            _abilityIds = abilites;

            _killstealAbilityMenu = killstealAbilityMenu;
            _killstealAbilityIds = killstealAbilities;
        }
    }
}

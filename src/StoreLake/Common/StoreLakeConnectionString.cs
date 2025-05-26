namespace StoreLake.TestStore
{
    using System;
    using System.Collections;

    internal sealed class StoreLakeConnectionString
    {
        private readonly string _usersConnectionString;
        private readonly Hashtable _parsetable;
        private readonly string _initialCatalog;

        public StoreLakeConnectionString(string connectionString)
        {
            _parsetable = new Hashtable();
            _usersConnectionString = (connectionString != null) ? connectionString : "";
            if (0 < _usersConnectionString.Length)
                ParseInternal(_parsetable, connectionString);

            _initialCatalog = ConvertValueToString("initial catalog", "");
        }

        private void ParseInternal(Hashtable parseTable, string connectionString)
        {
            // server=HL50JUS;database=Itil3System;uid=...;pwd=...;Persist Security Info=True
            // Data Source=BUILDBOT;Initial Catalog=ITIL3SYSTEM;Persist Security Info=True;User ID=...;Password=...
            string[] pairs = connectionString.Split(';');
            foreach (var pair in pairs)
            {
                string[] key_value = pair.Split('=');
                if (key_value.Length != 2)
                {
                    throw new InvalidOperationException("Wrong connection string item:" + key_value[0]);
                }

                if (string.Equals("initial catalog", key_value[0], StringComparison.OrdinalIgnoreCase))
                {
                    parseTable.Add(key_value[0].ToLowerInvariant(), key_value[1]);
                }
                else if (string.Equals("Data Source", key_value[0], StringComparison.OrdinalIgnoreCase)
                      || string.Equals("Persist Security Info", key_value[0], StringComparison.OrdinalIgnoreCase)
                      || string.Equals("User ID", key_value[0], StringComparison.OrdinalIgnoreCase)
                      || string.Equals("Password", key_value[0], StringComparison.OrdinalIgnoreCase)
                      )
                {
                    // ignore
                }
                else
                {
                    throw new InvalidOperationException("Unknown connection string item:" + key_value[0]);
                }
            }
        }

        public string InitialCatalog => _initialCatalog;

        public string UsersConnectionString(bool hidePassword)
        {
            return UsersConnectionString(hidePassword, forceHidePassword: false);
        }

        internal StoreLakeConnectionString SetUsersConnectionString(string connectionString)
        {
            return new StoreLakeConnectionString(connectionString);
        }

        private string UsersConnectionString(bool hidePassword, bool forceHidePassword)
        {
            string constr = _usersConnectionString;
            //if (HasPasswordKeyword && (forceHidePassword || (hidePassword && !HasPersistablePassword)))
            //{
            //    ReplacePasswordPwd(out constr, fakePassword: false);
            //}
            if (constr == null)
            {
                return "";
            }
            return constr;
        }

        public string ConvertValueToString(string keyName, string defaultValue)
        {
            string text = (string)_parsetable[keyName];
            if (text == null)
            {
                return defaultValue;
            }
            return text;
        }
    }
}

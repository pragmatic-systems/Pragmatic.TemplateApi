# Manual Steps for configuring keycloak via UI without config file. (Extracted from Readme)

### Login
Login is admin/password by default for local config.

### Configure Server

* Go to local **[Keycloak](https://localhost:8443/)**
* Go to the **`master`** dropdown → Create a new realm **`todolist`** (a realm can represent all users across multiple applications).
* In **realm settings**, set **Unmanaged Attributes** to `Only administrators can write`. This shows the Attributes table in user accounts for custom role configurations.
* In your new realm, create a client **`todolist-client`** → Enable **`Client authentication`**, **`Client authorization`**, and **`Direct access grants`**.
* Go to **Client Scopes** → **`todolist-client-dedicated`**:
  * **Add a roles mapper:** `Add mapper by configuration` → `User Attribute`. Name: `roles-mapper`, User Attribute: `roles`, Token Claim Name: `roles`, set **`Multivalued`** = true. This includes user `roles` attributes in the JWT.
  * **Add an audience mapper** and include the client name.
* Under **Client** → **Client Details** → **Credentials**, record the **`client secret`** for later.
* Create an app user **`todolist-user`** → Configure fully (first/last name, email required to activate), set and record the password, ensure it is **not transient** and has no pending actions.
* Add attributes to the user: (`roles`, `TodoList:Read`), (`roles`, `TodoList:Write`), and (`roles`, `Hangfire:Dashboard`).

### Keycloak – Generate JWT

Post: https://localhost:8443/realms/todolist/protocol/openid-connect/token

With URL form:
grant_type: password
client_id: todolist-client
client_secret: {clientsecret}
username: todolist-user
password: {userpassword}
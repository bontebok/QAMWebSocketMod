# QAMWebSocketMod
ResoniteModLoader Mod to update a QuadArrayMesh by using JSON values sent over a WebSocket connection.

## Usage

If using the Mod on a graphical client, ensure that the ResoniteModSettings Mod is also installed and edit the Allowed URIs property to include a list of all permitted URIs, or if you are on a headless client, edit the allowedUris value in the ```rml_config\QAMWebSocketMod.json``` file to include a comma separated list of allowed URIs for WebSocket connections from the Mod. Note: If this list is empty or the URI value is not found in the list, the connection will be blocked.

In Resonite, create a new Empty object and populated it with a Dynamic Variable Space named ```QAMWebSocket```. Ensure that Direct Binding is enabled.

Create the following three variables somewhere in that space -

```
QAMWebSocket/Uri <Uri>
QAMWebSocket/Slot <Slot>
QAMWebSocket/MeshRenderer <MeshRenderer>
```

For the Uri value variable, this value should be the URI this QuadArrayMesh will be connecting to and updating its data from. For the Slot reference variable, this will be the slot where the Mod will create the QuadArrayMesh onto, for the MeshRenderer reference variable, this will be the reference variable for the MeshRenderer that will be used to display the QuadArrayMesh.

Simply save and respawn this object to activate the Mod and open a WebSocket connection.

## TBD - Instructions on how to send QuadArrayMesh line updates.
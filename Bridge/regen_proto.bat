cd meshtastic
rename deviceonly.proto deviceonly.ignoreproto
cd ..

..\..\protoc.exe -I=protobufs --csharp_out=./Generated --csharp_opt=base_namespace=Meshtastic.Protobufs ./protobufs/meshtastic/*.proto
cd meshtastic
rename deviceonly.ignoreproto deviceonly.proto
cd ..\..\
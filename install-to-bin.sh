#!/usr/bin/env bash

echo "Installing to /usr/bin/apt-repo-builder"

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

sudo rm -rf /usr/bin/apt-repo-builder

echo "#!/usr/bin/env bash" | sudo tee -a /usr/bin/apt-repo-builder > /dev/null
echo "exec dotnet exec $DIR/src/AptRepoBuilder/bin/Debug/netcoreapp3.1/AptRepoBuilder.dll \$*" | sudo tee -a /usr/bin/apt-repo-builder > /dev/null
sudo chmod +x /usr/bin/apt-repo-builder

echo "Done!"

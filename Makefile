prefix = /usr/local

all:
	dotnet publish -c Release --self-contained true -r linux-x64 -o _build src/AptRepoBuilder/AptRepoBuilder.csproj

install:
	install -d $(DESTDIR)$(prefix)/share/apt-repo-builder
	cp -r _build/* $(DESTDIR)$(prefix)/share/apt-repo-builder
	install -d $(DESTDIR)$(prefix)/bin
	ln -s ../share/apt-repo-builder/AptRepoBuilder $(DESTDIR)$(prefix)/bin/apt-repo-builder

clean:
	rm -rf _build

distclean: clean

uninstall:
	rm -f $(DESTDIR)$(prefix)/bin/apt-repo-builder
	rm -rf $(DESTDIR)$(prefix)/share/apt-repo-builder

.PHONY: all install clean distclean uninstall
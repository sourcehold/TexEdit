using Gtk;
using System;
using System.IO;
using System.Collections.Generic;

class TxtEdit : Window
{
    struct Section {
        public string name;
        public List<string> contents;
    };

    private Int32[] offsets;
    private List<Section> sections;

    public TxtEdit() :  base("Stronghold .tex editor v.0.1") {
        offsets = new Int32[250];
        sections = new List<Section>();
        file_dialog();

        SetDefaultSize(800, 600);
        SetPosition(WindowPosition.Center);

        DeleteEvent += delegate {
            Application.Quit();
        };

        // menu bar

        MenuBar mb = new MenuBar();
        Menu filemenu = new Menu();
        Menu helpmenu = new Menu();

        MenuItem file = new MenuItem("File");
        file.Submenu = filemenu;

        MenuItem open = new MenuItem("Open");
        open.Activated += delegate {
            file_dialog();
        };
        filemenu.Append(open);

        MenuItem reload = new MenuItem("Reload");
        reload.Activated += delegate {
            MessageDialog md = new MessageDialog(this,
                                                 DialogFlags.DestroyWithParent, MessageType.Question,
                                                 ButtonsType.OkCancel, "This will undo all changes. Continue?");
            md.Run();
            md.Destroy();
        };
        filemenu.Append(reload);

        MenuItem exit = new MenuItem("Exit");
        exit.Activated += delegate {
            Application.Quit();
        };
        filemenu.Append(exit);

        MenuItem help = new MenuItem("Help");
        help.Submenu = helpmenu;

        MenuItem about = new MenuItem("About");
        about.Activated += about_event;
        helpmenu.Append(about);

        mb.Append(file);
        mb.Append(help);

        VBox vbox = new VBox(false, 2);
        vbox.PackStart(mb, false, false, 0);

        // tree view

        ScrolledWindow scroller = new ScrolledWindow();
        scroller.BorderWidth = 5;
        scroller.ShadowType = ShadowType.In;
        vbox.Add(scroller);

        Gtk.TreeView tree = new Gtk.TreeView();
        scroller.Add(tree);

        Gtk.TreeViewColumn sectionColumn = new Gtk.TreeViewColumn();
        sectionColumn.Title = "Section";
        Gtk.CellRendererText sectionCell = new Gtk.CellRendererText();
        sectionColumn.PackStart(sectionCell, true);

        sectionColumn.AddAttribute(sectionCell, "text", 0);

        tree.AppendColumn(sectionColumn);

        Gtk.TreeStore store = new Gtk.TreeStore(typeof (string));

        foreach(var sec in sections) {
            Gtk.TreeIter iter = store.AppendValues(sec.name);
            store.AppendValues(iter, "value");
        }

        tree.Model = store;

        Add(vbox);

        ShowAll();
    }

    private void about_event(object sender, EventArgs args) {
        AboutDialog about = new AboutDialog();
        about.ProgramName = "TexEdit";
        about.Version = "0.1";
        about.Copyright = "(C) Julian Offenhäuser";
        about.Comments = @"A tool for editing ingame text in Stronghold";
        about.Website = "https://www.github.com/sourcehold/TexEdit";
        about.Run();
        about.Destroy();
    }

    private void file_dialog() {
        Gtk.FileChooserDialog filechooser =
            new Gtk.FileChooserDialog("Pick the sh.tex",
                                      this,
                                      FileChooserAction.Open,
                                      "Cancel", ResponseType.Cancel,
                                      "Open", ResponseType.Accept);

        Gtk.FileFilter filter = new Gtk.FileFilter();
        filter.Name = "tex";
        filter.AddPattern("sh.tex");
        filechooser.Filter = filter;

        if(filechooser.Run() == (int)ResponseType.Accept) {
            FileStream fs = null;

            try {
                fs = new FileStream(filechooser.Filename, FileMode.Open);
                BinaryReader r = new BinaryReader(fs);

                // read offsets
                for(int i = 0; i < 250; i++) {
                    offsets[i] = 0x3e8 + (r.ReadInt32() << 1);
                }

                // read strings
                for(int i = 0; i < 250; i++) {
                    r.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
                    string name = "";
                    while(true) { // todo: PeekChar() throws an exception, thanks C#!
                        byte[] b = r.ReadBytes(2);

                        byte hi = b[1];
                        byte lo = b[0];

                        int wchar = hi << 8 | lo;
                        if(wchar == 0) break;

                        name += (char)wchar;
                    }

                    Section sec = new Section();
                    sec.name = name;

                    sections.Add(sec);
                }
            }
            catch (IOException)
            {
                return;
            } finally {
                fs.Close();
            }
         }

        filechooser.Destroy();
    }

    static void Main () {
        Application.Init();
        new TxtEdit();
        Application.Run();
    }
}

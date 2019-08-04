using Gtk;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class TexString {
    public TexString() {}
    public UInt32 Index; // index into the offset table
    public string Text;
};

class TxtEdit : Window
{
    private UInt32[] offsets;
    private List<TexString> sections;
    private Gtk.TreeStore store;
    private string filepath;

    public TxtEdit() :  base("Stronghold .tex editor v.0.2") {
        offsets = new UInt32[250];
        sections = new List<TexString>();
        file_dialog();

        SetDefaultSize(800, 600);
        SetPosition(WindowPosition.Center);

        DeleteEvent += delegate {
            Application.Quit();
        };

        Gtk.TreeView tree = new Gtk.TreeView();

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
            ResponseType result = (ResponseType)md.Run();
            if (result == ResponseType.Ok) {
                sections = new List<TexString>();

                load_file();

                store = new Gtk.TreeStore(typeof(TexString));
                foreach(var sec in sections) {
                    store.AppendValues(sec);
                }
                tree.Model = store;
            }

            md.Destroy();
        };
        filemenu.Append(reload);

        MenuItem save = new MenuItem("Save");
        save.Activated += delegate {
            save_backup();
            save_tex();
        };
        filemenu.Append(save);

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

        // scrolling area
        ScrolledWindow scroller = new ScrolledWindow();
        scroller.BorderWidth = 5;
        scroller.ShadowType = ShadowType.In;
        vbox.Add(scroller);

        // tree view
        Gtk.TreeViewColumn sectionColumn = new Gtk.TreeViewColumn();
        sectionColumn.Title = "Text";

        Gtk.CellRendererText sectionCell = new Gtk.CellRendererText();
        sectionCell.Editable = true;
        sectionCell.Edited += sectionCell_edit;
        sectionColumn.PackStart(sectionCell, true);

        store = new Gtk.TreeStore(typeof(TexString));
        foreach(var sec in sections) {
            store.AppendValues(sec);
        }

        sectionColumn.SetCellDataFunc(sectionCell, new Gtk.TreeCellDataFunc(RenderSection));

        tree.AppendColumn(sectionColumn);
        tree.Model = store;
        scroller.Add(tree);

        Add(vbox);

        ShowAll();
    }

    private void RenderSection(Gtk.TreeViewColumn column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter) {
        TexString sec = (TexString)model.GetValue(iter, 0);
        (cell as Gtk.CellRendererText).Text = sec.Text;
    }

    private void sectionCell_edit(object o, Gtk.EditedArgs args) {
        Gtk.TreeIter iter;
        store.GetIter (out iter, new Gtk.TreePath (args.Path));

        TexString sec = (TexString)store.GetValue(iter, 0);
        sec.Text = args.NewText;
    }

    private void about_event(object sender, EventArgs args) {
        AboutDialog about = new AboutDialog();
        about.ProgramName = "TexEdit";
        about.Version = "0.2";
        about.Copyright = "(C) Julian Offenhäuser";
        about.Comments = @"A tool for editing ingame text in Stronghold";
        about.Website = "https://github.com/sourcehold/TexEdit";
        about.Run();
        about.Destroy();
    }

    private void save_backup() {
        File.Copy(filepath, filepath + ".bak", true);
    }

    private void save_tex() {
        // write strings
        FileStream fs = null;

        try {
            System.Text.Encoding encoding = System.Text.Encoding.Unicode;

            fs = new FileStream(filepath, FileMode.Open, FileAccess.Write);
            BinaryWriter w = new BinaryWriter(fs, encoding);

            w.Seek(0x3e8, SeekOrigin.Begin);

            UInt32 currentIndex = 0;
            foreach(var sec in sections) {
                UInt32 index = sec.Index;
                string text = sec.Text;

                if(currentIndex != index) {
                    w.Write((UInt32)0);
                    w.Write((UInt16)0);
                    offsets[index] = (UInt32)((w.BaseStream.Position - 0x3e8) / 2);
                }
                currentIndex = index;

                // write the string
                w.Write(text.ToCharArray());
                w.Write((UInt32)0);
            }
            w.Seek(0, SeekOrigin.Begin);

            foreach(UInt32 offset in offsets) {
                w.Write(offset);
            }
        } finally {
            fs.Close();
        }
    }

    private void load_file() {
        FileStream fs = null;

        try {
            fs = new FileStream(filepath, FileMode.Open);
            BinaryReader r = new BinaryReader(fs);

            // read offsets
            for(int i = 0; i < 250; i++) {
                offsets[i] = r.ReadUInt32();
            }

            // read strings (TODO, a bit bodged)
            for(UInt32 i = 0; i < 250; i++) {
                TexString sec = new TexString();
                UInt32 end;
                string text = "";

                if(i == 249) {
                    end = 30000; // TODO
                } else {
                    end = ((offsets[i+1] - offsets[i]) * 2);
                }

                r.BaseStream.Seek((0x3e8 + offsets[i]*2), SeekOrigin.Begin);
                for(UInt32 rp = 0; rp < end; rp += 2) {
                    byte[] b = r.ReadBytes(2);

                    if(b[0] == 0 && b[1] == 0) {
                        if(text.Length > 1) {
                            sec.Text = text;
                            sec.Index = i;
                            sections.Add(sec);
                        }
                        text = "";
                        sec = new TexString();
                    }else {
                        text += System.Text.Encoding.Unicode.GetString(b);
                    }
                }
            }
        }
        catch (IOException)
        {
            return;
        } finally {
            fs.Close();
        }
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
            filepath = filechooser.Filename;
            load_file();
         }

        filechooser.Destroy();
    }

    static void Main () {
        Application.Init();
        new TxtEdit();
        Application.Run();
    }
}

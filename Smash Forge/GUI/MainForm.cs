﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using WeifenLuo.WinFormsUI.Docking;
using OpenTK;
using System.Data;
using Octokit;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualBasic.Devices;

namespace Smash_Forge
{
    public partial class MainForm : Form
    {

        public static MainForm Instance { get { return _instance != null ? _instance : (_instance = new MainForm()); } }
        private static MainForm _instance;

        public WorkspaceManager Workspace { get; set; }

        public String[] filesToOpen = null;
        public static string executableDir = null;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ThreadStart t = new ThreadStart(Smash_Forge.Update.CheckLatest);
            Thread thread = new Thread(t);
            thread.Start();
            Runtime.renderDepth = 2500f;
            foreach (var vp in viewports)
                AddDockedControl(vp);

            animationsWindowToolStripMenuItem.Checked =
            boneTreeToolStripMenuItem.Checked = true;

            Runtime.acmdEditor = new ACMDPreviewEditor() { ShowHint = DockState.DockRight };
            AddDockedControl(Runtime.acmdEditor);

            AddDockedControl(leftPanel);
            AddDockedControl(rightPanel);
            AddDockedControl(lvdEditor);
            AddDockedControl(lvdList);
            AddDockedControl(project);
            AddDockedControl(meshList);
            rightPanel.treeView1.Nodes.Add(animNode);
            rightPanel.treeView1.Nodes.Add(mtaNode);
            Runtime.renderBones = true;
            Runtime.renderLVD = true;
            Runtime.renderFloor = true;
            Runtime.renderHitboxes = true;
            Runtime.renderModel = true;
            Runtime.renderPath = true;
            Runtime.renderCollisions = true;
            Runtime.renderCollisionNormals = false;
            Runtime.renderGeneralPoints = true;
            Runtime.renderItemSpawners = true;
            Runtime.renderSpawns = true;
            Runtime.renderRespawns = true;
            Runtime.renderOtherLVDEntries = true;
            Runtime.renderNormals = true;
            Runtime.renderVertColor = true;
            Runtime.renderSwag = false;
            Runtime.renderType = Runtime.RenderTypes.Texture;
            //Pichu.MakePichu();
            //meshList.refresh();
            //ReadChimeraLucas();
            viewportWindowToolStripMenuItem.Checked = true;
            if(filesToOpen != null)
            {
                foreach (String file in filesToOpen)
                    openFile(file);
                filesToOpen = null;
            }
        }

        private void MainForm_Close(object sender, EventArgs e)
        {
            if (Runtime.TargetNUD != null)
                Runtime.TargetNUD.Destroy();

            foreach (ModelContainer n in Runtime.ModelContainers)
            {
                n.Destroy();
            }
            foreach (NUT n in Runtime.TextureContainers)
            {
                n.Destroy();
            }
        }

        public void AddDockedControl(DockContent content)
        {
            if (dockPanel1.DocumentStyle == DocumentStyle.SystemMdi)
            {
                content.MdiParent = this;
                content.Show();
            }
            else
                content.Show(dockPanel1);
        }

        #region Members
        public AnimListPanel rightPanel = new AnimListPanel() { ShowHint = DockState.DockRight };
        public BoneTreePanel leftPanel = new BoneTreePanel() { ShowHint = DockState.DockLeft };
        public static TreeNode animNode = new TreeNode("Bone Animations");
        public TreeNode mtaNode = new TreeNode("Material Animations");
        public ProjectTree project = new ProjectTree() { ShowHint = DockState.DockLeft };
        public LVDList lvdList = new LVDList() { ShowHint = DockState.DockLeft };
        public LVDEditor lvdEditor = new LVDEditor() { ShowHint = DockState.DockRight };
        public List<PARAMEditor> paramEditors = new List<PARAMEditor>() { };
        public List<MTAEditor> mtaEditors = new List<MTAEditor>() { };
        public List<ACMDEditor> ACMDEditors = new List<ACMDEditor>() { };
        public List<SwagEditor> SwagEditors = new List<SwagEditor>() { };
        public MeshList meshList = new MeshList() { ShowHint = DockState.DockRight };
        public List<VBNViewport> viewports = new List<VBNViewport>() { new VBNViewport() }; // Default viewport
        #endregion

        #region ToolStripMenu
        private void openNUDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PARAMEditor currentParam = null;
            ACMDEditor currentACMD = null;
            SwagEditor currentSwagEditor = null;
            foreach (PARAMEditor p in paramEditors)
                if (p.ContainsFocus)
                    currentParam = p;
            
            foreach (ACMDEditor a in ACMDEditors)
                if (a.ContainsFocus)
                    currentACMD = a;
            
            foreach (SwagEditor s in SwagEditors)
                if (s.ContainsFocus)
                    currentSwagEditor = s;
            
            if (currentParam != null)
                currentParam.saveAs();
            else if (currentACMD != null)
                currentACMD.save();
            else if (currentSwagEditor != null)
                currentSwagEditor.save();
            else
            {
                string filename = "";
                SaveFileDialog save = new SaveFileDialog();
                save.Filter = "Supported Filetypes (VBN,LVD)|*.vbn;*.lvd;*.dae|Smash 4 Boneset|*.vbn|All files(*.*)|*.*";
                DialogResult result = save.ShowDialog();

                if (result == DialogResult.OK)
                {
                    filename = save.FileName;
                    if (filename.EndsWith(".vbn"))
                    {
                        Runtime.TargetVBN.Endian = Endianness.Big;
                        if (!checkBox1.Checked)
                            Runtime.TargetVBN.Endian = Endianness.Little;
                        Runtime.TargetVBN.Save(filename);
                    }
                    if (filename.EndsWith(".lvd") && Runtime.TargetLVD != null)
                        File.WriteAllBytes(filename, Runtime.TargetLVD.Rebuild());
                    else if (filename.EndsWith(".lvd"))
                    {
                        DAT d = null;
                        foreach (ModelContainer c in Runtime.ModelContainers)
                            if (c.dat_melee != null)
                                d = c.dat_melee;
                        if (d != null)
                        {
                            DialogResult r = MessageBox.Show("Would you like to save in safe mode?\n(This is not suggested, only use when needed)", "DAT -> LVD safe mode", MessageBoxButtons.YesNo);
                            if (r == DialogResult.Yes)
                            {
                                File.WriteAllBytes(filename, d.toLVD(true).Rebuild());
                            }
                            else if (r == DialogResult.No)
                            {
                                File.WriteAllBytes(filename, d.toLVD(false).Rebuild());
                            }
                        }

                    }

                    if (filename.EndsWith(".dae"))
                    {
                        if (Runtime.ModelContainers.Count > 0)
                        {
                            Collada.Save(filename, Runtime.ModelContainers[0]);
                        }
                    }
                    //OMO.createOMO (anim, vbn, "C:\\Users\\ploaj_000\\Desktop\\WebGL\\test_outut.omo", -1, -1);
                }
            }
        }

        public static void DAEReadSemantic(int p, Dictionary<string, string> semantic)
        {

        }

        private void openNud(string filename, string name = "")
        {
            string[] files = Directory.GetFiles(System.IO.Path.GetDirectoryName(filename));

            string pnud = filename;
            string pnut = "";
            string pjtb = "";
            string pvbn = "";
            string pmta = "";
            string psb = "";
            string pmoi = "";
            List<string> pacs = new List<string>();

            foreach (string s in files)
            {
                if (s.EndsWith(".nut"))
                    pnut = s;
                if (s.EndsWith(".vbn"))
                    pvbn = s;
                if (s.EndsWith(".jtb"))
                    pjtb = s;
                if (s.EndsWith(".mta"))
                    pmta = s;
                if (s.EndsWith(".sb"))
                    psb = s;
                if (s.EndsWith(".moi"))
                    pmoi = s;
                if (s.EndsWith(".pac"))
                    pacs.Add(s);
            }

            ModelContainer model = new ModelContainer();
            model.name = name;
            if (!pvbn.Equals(""))
            {
                model.vbn = new VBN(pvbn);
                Runtime.TargetVBN = model.vbn;
                if (!pjtb.Equals(""))
                    model.vbn.readJointTable(pjtb);
                if (!psb.Equals(""))
                    model.vbn.swingBones.Read(psb);
            }

            if (!pnut.Equals(""))
            {
                NUT nut = new NUT(pnut);
                Runtime.TextureContainers.Add(nut);
            }

            if (!pnud.Equals(""))
            {
                model.nud = new NUD(pnud);

                //AddDockedControl(new NUDMaterialEditor(model.nud.mesh[0].polygons[0].materials));

                foreach (string s in pacs)
                {
                    PAC p = new PAC();
                    p.Read(s);
                    byte[] data;
                    p.Files.TryGetValue("default.mta", out data);
                    if (data != null)
                    {
                        MTA m = new MTA();
                        m.read(new FileData(data));
                        model.nud.applyMTA(m, 0);
                    }
                }
            }

            if (!pmta.Equals(""))
            {
                try
                {
                    model.mta = new MTA();
                    model.mta.Read(pmta);
                    string mtaName = Path.Combine(Path.GetFileName(Path.GetDirectoryName(pmta)),Path.GetFileName(pmta));
                    Console.WriteLine($"MTA Name - {mtaName}");
                    addMaterialAnimation(mtaName, model.mta);
                }
                catch (EndOfStreamException)
                {
                    model.mta = null;
                }
            }

            if (!pmoi.Equals(""))
            {
                model.moi = new MOI(pmoi);
            }

            if (model.nud != null)
            {
                model.nud.MergePoly();
            }

            Runtime.ModelContainers.Add(model);
            meshList.refresh();
        }

        private void addMaterialAnimation(string name, MTA m)
        {
            if (!Runtime.MaterialAnimations.ContainsValue(m) && !Runtime.MaterialAnimations.ContainsKey(name))
                Runtime.MaterialAnimations.Add(name, m);
            viewports[0].loadMTA(m);
            mtaNode.Nodes.Add(name);
        }

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Supported Formats|*.omo;*.anim;*.chr0;*.smd;*.mta;*.pac;*.dat|" +
                             "Object Motion|*.omo|" +
                             "Maya Animation|*.anim|" +
                             "NW4R Animation|*.chr0|" +
                             "Source Animation (SMD)|*.smd|" +
                             "Smash 4 Material Animation (MTA)|*.mta|" +
                             "All files(*.*)|*.*";

                ofd.Multiselect = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                    foreach(string filename in ofd.FileNames)
                        openAnimation(filename);
                
            }
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {

            /*if (Runtime.TargetVBN == null)
            {
                return;
            }*/

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Supported Files (.omo, .anim, .pac, .mta)|*.omo;*.anim;*.pac;*.mta|" +
                             "Object Motion (.omo)|*.omo|" +
                             "Material Animation (.mta)|*.mta|" +
                             "Maya Anim (.anim)|*.anim|" +
                             "OMO Pack (.pac)|*.pac|" +
                             "All Files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    sfd.FileName = sfd.FileName;

                    if (sfd.FileName.EndsWith(".anim"))
                    {
                        if (Runtime.TargetAnim.Tag is AnimTrack)
                            ((AnimTrack)Runtime.TargetAnim.Tag).createANIM(sfd.FileName, Runtime.TargetVBN);
                        else
                            ANIM.createANIM(sfd.FileName, Runtime.TargetAnim, Runtime.TargetVBN);
                    }

                    if (sfd.FileName.EndsWith(".omo"))
                    {
                        if (Runtime.TargetAnim.Tag is FileData)
                        {
                            FileOutput o = new FileOutput();
                            o.writeBytes(((FileData)Runtime.TargetAnim.Tag).getSection(0, ((FileData)Runtime.TargetAnim.Tag).size()));
                            o.save(sfd.FileName);
                        }
                        else
                            OMO.createOMO(Runtime.TargetAnim, Runtime.TargetVBN, sfd.FileName);
                    }

                    if (sfd.FileName.EndsWith(".pac"))
                    {
                        var pac = new PAC();
                        foreach (var anim in Runtime.Animations)
                        {
                            var bytes = OMO.createOMO(anim.Value, Runtime.TargetVBN);
                            if (Runtime.TargetAnim.Tag is FileData)
                                bytes = ((FileData)Runtime.TargetAnim.Tag).getSection(0, ((FileData)Runtime.TargetAnim.Tag).size());

                            pac.Files.Add(anim.Key, bytes);
                        }
                        pac.Save(sfd.FileName);
                    }

                    if (sfd.FileName.EndsWith(".mta"))
                    {
                        FileOutput f = new FileOutput();
                        f.writeBytes(Runtime.TargetMTA.Rebuild());
                        f.save(sfd.FileName);
                    }
                }
            }
        }
        private void hashMatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HashMatch();
            //leftPanel.treeRefresh();
        }
        public static void HashMatch()
        {
            csvHashes csv = new csvHashes(Path.Combine(executableDir, "hashTable.csv"));
            foreach (ModelContainer m in Runtime.ModelContainers)
            {
                if (m.vbn != null)
                {
                    foreach (Bone bone in m.vbn.bones)
                    {
                        for (int i = 0; i < csv.names.Count; i++)
                        {
                            if (csv.names[i] == new string(bone.boneName))
                            {
                                bone.boneId = csv.ids[i];
                            }
                        }
                        if (bone.boneId == 0)
                            bone.boneId = Crc32.Compute(new string(bone.boneName));
                    }
                }

                if (m.dat_melee != null)
                {
                    foreach (Bone bone in m.dat_melee.bones.bones)
                    {
                        for (int i = 0; i < csv.names.Count; i++)
                        {
                            if (csv.names[i] == new string(bone.boneName))
                            {
                                bone.boneId = csv.ids[i];
                            }
                        }
                        if (bone.boneId == 0)
                            bone.boneId = Crc32.Compute(new string(bone.boneName));
                    }
                }
                if (m.bch != null)
                {
                    foreach (BCH.BCH_Model mod in m.bch.models)
                    {
                        foreach (Bone bone in mod.skeleton.bones)
                        {
                            for (int i = 0; i < csv.names.Count; i++)
                            {
                                if (csv.names[i] == new string(bone.boneName))
                                {
                                    bone.boneId = csv.ids[i];
                                }
                            }
                        }
                    }
                }
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var abt = new About())
            {
                abt.ShowDialog();
            }
        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            animNode.Nodes.Clear();
            mtaNode.Nodes.Clear();
            Runtime.Animations.Clear();
            Runtime.MaterialAnimations.Clear();
            Runtime.TargetVBN.reset();
        }

        private void importToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog() { Filter = "Motion Table (.mtable)|*.mtable|All Files (*.*)|*.*" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Runtime.Moveset = new MovesetManager(ofd.FileName);
                }
            }
        }
        private void animationPanelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rightPanel.Show(dockPanel1);
        }
        private void viewportWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (viewportWindowToolStripMenuItem.Checked == false)
            {
                var vp = new VBNViewport();
                viewports.Add(vp);
                AddDockedControl(vp);
                viewportWindowToolStripMenuItem.Checked = true;
            }

        }

        private void animationsWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (animationsWindowToolStripMenuItem.Checked)
                rightPanel.Show(dockPanel1);
            else
                rightPanel.Hide();
        }

        private void boneTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (boneTreeToolStripMenuItem.Checked)
                leftPanel.Show(dockPanel1);
            else
                leftPanel.Hide();
        }
        #endregion
        
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            Runtime.TargetVBN.Endian = Endianness.Big;
            Runtime.TargetVBN.unk_1 = 1;
            Runtime.TargetVBN.unk_2 = 2;
        }
        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            Runtime.TargetVBN.Endian = Endianness.Little;
            Runtime.TargetVBN.unk_1 = 2;
            Runtime.TargetVBN.unk_2 = 1;
        }

        public void openMats(NUD.Polygon poly, string name)
        {
            (new NUDMaterialEditor(poly) { ShowHint = DockState.Float, Text = name }).Show();
        }

        private void clearWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Runtime.killWorkspace = true;
        }
        private void renderSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {

            using (var rndr = new GUI.RenderSettings())
            {
                rndr.ShowDialog();
            }
        }
        private void meshListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            meshList.refresh();
            AddDockedControl(meshList);
        }
        private void projectTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (project.DockState == DockState.Unknown)
                project = new ProjectTree();
            else
                project.Focus();
        }

        private void openCharacterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var ofd = new FolderSelectDialog())
            {
                ofd.Title = "Character Folder";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    project.openACMD($"{ofd.SelectedPath}\\script\\animcmd\\body\\motion.mtable", $"{ofd.SelectedPath}\\motion");
                }
            }
        }

        private void saveNUDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Runtime.ModelContainers.Count > 0)
            {
                string filename = "";
                SaveFileDialog save = new SaveFileDialog();
                save.Filter = "Namco Universal Data|*.nud|All files(*.*)|*.*";
                DialogResult result = save.ShowDialog();

                if (result == DialogResult.OK)
                {
                    filename = save.FileName;
                    if (filename.EndsWith(".nud"))
                        if (Runtime.ModelContainers[0].dat_melee != null)
                        {
                            ModelContainer m = Runtime.ModelContainers[0].dat_melee.wrapToNUD();
                            m.nud.Save(filename);
                            m.vbn.Save(filename.Replace(".nud", ".vbn"));
                        }
                        else
                            Runtime.ModelContainers[0].nud.Save(filename);
                }
            }
        }

        private void collisionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetLVD == null)
                Runtime.TargetLVD = new LVD();
            Runtime.TargetLVD.collisions.Add(new Collision() { name = "COL_00_NewCollision", subname = "00_NewCollision" });
            lvdList.fillList();
        }

        private void spawnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetLVD == null)
                Runtime.TargetLVD = new LVD();
            Runtime.TargetLVD.spawns.Add(new Point() { name = "START_00_NEW", subname = "00_NEW" });
            lvdList.fillList();

        }

        private void respawnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetLVD == null)
                Runtime.TargetLVD = new LVD();
            Runtime.TargetLVD.respawns.Add(new Point() { name = "RESPAWN_00_NEW", subname = "00_NEW" });
            lvdList.fillList();
        }

        private void cameraBoundsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetLVD == null)
                Runtime.TargetLVD = new LVD();
            Runtime.TargetLVD.cameraBounds.Add(new Bounds() { name = "CAMERA_00_NEW", subname = "00_NEW" });
            lvdList.fillList();

        }

        private void blastzonesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetLVD == null)
                Runtime.TargetLVD = new LVD();
            Runtime.TargetLVD.blastzones.Add(new Bounds() { name = "DEATH_00_NEW", subname = "00_NEW" });
            lvdList.fillList();
        }

        private void itemSpawnerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetLVD == null)
                Runtime.TargetLVD = new LVD();
            Runtime.TargetLVD.items.Add(new ItemSpawner() { name = "ITEM_00_NEW", subname = "00_NEW" });
            lvdList.fillList();
        }

        private void generalPointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetLVD == null)
                Runtime.TargetLVD = new LVD();
            Runtime.TargetLVD.generalPoints.Add(new Point() { name = "POINT_00_NEW", subname = "00_NEW" });
            lvdList.fillList();
        }

        private void pointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GeneralPoint g = new GeneralPoint() { name = "POINT_00_NEW", subname = "00_NEW" };
            Runtime.TargetLVD.generalShapes.Add(g);
            lvdList.fillList();
        }

        private void rectangleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GeneralRect g = new GeneralRect() { name = "RECT_00_NEW", subname = "00_NEW" };
            Runtime.TargetLVD.generalShapes.Add(g);
            lvdList.fillList();
        }

        private void pathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GeneralPath g = new GeneralPath() { name = "PATH_00_NEW", subname = "00_NEW" };
            Runtime.TargetLVD.generalShapes.Add(g);
            lvdList.fillList();
        }

        private void openNUTEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NUTEditor ev = new NUTEditor();
            ev.Show();
        }

        private void exportToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (var ofd = new FolderSelectDialog())
            {
                ofd.Title = "Export ACMD Folder";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Runtime.Moveset.MotionTable.Export(ofd.SelectedPath + "\\motion.mtable");
                    Runtime.Moveset.Game.Export(ofd.SelectedPath + "\\game.bin");
                    Runtime.Moveset.Sound.Export(ofd.SelectedPath + "\\sound.bin");
                    Runtime.Moveset.Expression.Export(ofd.SelectedPath + "\\expression.bin");
                    Runtime.Moveset.Effect.Export(ofd.SelectedPath + "\\effect.bin");
                }
            }
        }

        private void deleteLVDEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lvdList.deleteSelected();
        }

        private void openStageToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (var ofd = new FolderSelectDialog())
            {
                ofd.Title = "Stage Folder";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string stagePath = ofd.SelectedPath;
                    string modelPath = stagePath + "\\model\\";
                    string paramPath = stagePath + "\\param\\";
                    string animationPath = stagePath + "\\animation\\";
                    List<string> nuds = new List<string>();

                    if (Directory.Exists(modelPath))
                    {
                        foreach (string d in Directory.GetDirectories(modelPath))
                        {
                            foreach (string f in Directory.GetFiles(d))
                            {
                                if (f.EndsWith(".nud"))
                                {
                                    openNud(f, Path.GetFileName(d));
                                }
                            }
                        }
                    }

                    if (Directory.Exists(paramPath))
                    {
                        Runtime.TargetLVD = null;
                        foreach (string f in Directory.GetFiles(paramPath))
                        {
                            if (Path.GetExtension(f).Equals(".lvd") && Runtime.TargetLVD == null)
                            {
                                Runtime.TargetLVD = new LVD(f);
                                lvdList.fillList();
                            }
                        }
                    }

                    if (Directory.Exists(animationPath))
                    {
                        foreach (string d in Directory.GetDirectories(animationPath))
                        {
                            foreach (string f in Directory.GetFiles(d))
                            {
                                if (f.EndsWith(".omo"))
                                {
                                    Runtime.Animations.Add(f, OMO.read(new FileData(f)));
                                    animNode.Nodes.Add(f);
                                }
                                else if (f.EndsWith("path.bin"))
                                {
                                    Runtime.TargetPath = new PathBin();
                                    Runtime.TargetPath.Read(f);
                                }
                            }
                        }
                    }
                }
            }

        }

        //<summary>
        //Open an animation based on filename
        //</summary>
        public void openAnimation(string filename)
        {

            //Runtime.Animations.Clear();
            if (filename.EndsWith(".mta"))
            {
                MTA mta = new MTA();
                try
                {
                    mta.Read(filename);
                    Runtime.MaterialAnimations.Add(filename, mta);
                    mtaNode.Nodes.Add(filename);
                    MainForm.Instance.viewports[0].loadMTA(mta);
                    Runtime.TargetMTAString = filename;
                }
                catch (EndOfStreamException)
                {
                    mta = null;
                }
            }
            else if (filename.EndsWith(".smd"))
            {
                var anim = new SkelAnimation();
                if (Runtime.TargetVBN == null)
                    Runtime.TargetVBN = new VBN();
                SMD.read(filename, anim, Runtime.TargetVBN);
                leftPanel.treeRefresh();
                Runtime.Animations.Add(filename, anim);
                animNode.Nodes.Add(filename);
            }
            else if (filename.EndsWith(".pac"))
            {
                PAC p = new PAC();
                p.Read(filename);

                foreach (var pair in p.Files)
                {
                    if (pair.Key.EndsWith(".omo"))
                    {
                        var anim = OMO.read(new FileData(pair.Value));
                        string AnimName = Regex.Match(pair.Key, @"([A-Z][0-9][0-9])(.*)").Groups[0].ToString();
                        //AnimName = pair.Key;
                        //AnimName = AnimName.Remove(AnimName.Length - 4);
                        //AnimName = AnimName.Insert(3, "_");
                        if (!string.IsNullOrEmpty(AnimName))
                        {
                            if (Runtime.Animations.ContainsKey(AnimName))
                                Runtime.Animations[AnimName].children.Add(anim);
                            else
                            {
                                animNode.Nodes.Add(AnimName);
                                Runtime.Animations.Add(AnimName, anim);
                            }
                        }
                        else
                        {
                            if (Runtime.Animations.ContainsKey(pair.Key))
                                Runtime.Animations[pair.Key].children.Add(anim);
                            else
                            {
                                animNode.Nodes.Add(pair.Key);
                                Runtime.Animations.Add(pair.Key, anim);
                            }
                        }
                    }
                    else if (pair.Key.EndsWith(".mta"))
                    {
                        MTA mta = new MTA();
                        try
                        {
                            if (!Runtime.MaterialAnimations.ContainsKey(pair.Key))
                            {
                                mta.read(new FileData(pair.Value));
                                Runtime.MaterialAnimations.Add(pair.Key, mta);
                                mtaNode.Nodes.Add(pair.Key);
                            }

                            // matching
                            string AnimName = Regex.Match(pair.Key, @"([A-Z][0-9][0-9])(.*)").Groups[0].ToString().Replace(".mta", ".omo");
                            if (Runtime.Animations.ContainsKey(AnimName))
                            {
                                Runtime.Animations[AnimName].children.Add(mta);
                            }

                        }
                        catch (EndOfStreamException)
                        {
                            mta = null;
                        }
                    }
                }
            }

            if (filename.EndsWith(".dat"))
            {
                if (!filename.EndsWith("AJ.dat"))
                    MessageBox.Show("Not a DAT animation");
                else
                {
                    if (Runtime.ModelContainers[0].dat_melee == null)
                        MessageBox.Show("Load a DAT model first");
                    else
                        DAT_Animation.LoadAJ(filename, Runtime.ModelContainers[0].dat_melee.bones);
                }

            }
            //if (Runtime.TargetVBN.bones.Count > 0)
            //{
            if (filename.EndsWith(".omo"))
            {
                Runtime.Animations.Add(filename, OMO.read(new FileData(filename)));
                animNode.Nodes.Add(filename);
            }
            if (filename.EndsWith(".chr0"))
            {
                Runtime.Animations.Add(filename, CHR0.read(new FileData(filename), Runtime.TargetVBN));
                animNode.Nodes.Add(filename);
            }
            if (filename.EndsWith(".anim"))
            {
                Runtime.Animations.Add(filename, ANIM.read(filename, Runtime.TargetVBN));
                animNode.Nodes.Add(filename);
            }
        }

        ///<summary>
        ///Open a file based on the filename
        ///</summary>
        /// <param name="filename"> Filename of file to open</param>
        public void openFile(string filename)
        {
            if(!filename.EndsWith(".mta") && !filename.EndsWith(".dat") && !filename.EndsWith(".smd"))
                openAnimation(filename);
            
            if (filename.EndsWith(".vbn"))
            {
                Runtime.TargetVBN = new VBN(filename);

                if (Directory.Exists("Skapon\\"))
                {
                    NUD nud = Skapon.Create(Runtime.TargetVBN);
                    ModelContainer con = new ModelContainer();
                    con.vbn = Runtime.TargetVBN;
                    con.nud = nud;
                    nud.PreRender();
                    Runtime.ModelContainers.Add(con);
                }
            }

            if (filename.EndsWith(".sb"))
            {
                SB sb = new SB();
                sb.Read(filename);
                SwagEditor swagEditor = new SwagEditor(sb) { ShowHint = DockState.DockRight };
                AddDockedControl(swagEditor);
                SwagEditors.Add(swagEditor);
            }

            if (filename.EndsWith(".dat"))
            {
                if (filename.EndsWith("AJ.dat"))
                {
                    MessageBox.Show("This is animation; load with Animation -> Import");
                    return;
                }
                DAT dat = new DAT();
                dat.Read(new FileData(filename));
                ModelContainer c = new ModelContainer();
                Runtime.ModelContainers.Add(c);
                c.dat_melee = dat;
                dat.PreRender();

                HashMatch();
                
                Runtime.TargetVBN = dat.bones;

                DAT_TreeView p = new DAT_TreeView() { ShowHint = DockState.DockLeft };
                p.setDAT(dat);
                AddDockedControl(p);
                //Runtime.TargetVBN = dat.bones;
                meshList.refresh();
            }

            if (filename.EndsWith(".nut"))
            {
                Runtime.TextureContainers.Add(new NUT(filename));
                NUTEditor ev = new NUTEditor();
                ev.Show();
            }

            if (filename.EndsWith(".lvd"))
            {
                Runtime.TargetLVD = new LVD(filename);
                LVD test = Runtime.TargetLVD;
                lvdList.fillList();
            }

            if (filename.EndsWith(".mta"))
            {
                Runtime.TargetMTA = new MTA();
                Runtime.TargetMTA.Read(filename);
                viewports[0].loadMTA(Runtime.TargetMTA);
                MTAEditor temp = new MTAEditor(Runtime.TargetMTA) { ShowHint = DockState.DockLeft };
                temp.Text = Path.GetFileName(filename);
                AddDockedControl(temp);
                mtaEditors.Add(temp);
            }

            if (filename.EndsWith(".mtable"))
            {
                //project.openACMD(filename);
                Runtime.Moveset = new MovesetManager(filename);
            }

            if (filename.EndsWith("path.bin"))
            {
                Runtime.TargetPath = new PathBin(filename);
            }
            else
            if (filename.EndsWith(".bin"))
            {
                //Note to whoever is readin this: 
                //Eventually we need to look at the magic here (and also make all .bins look at magic)
                //Runtime.TargetCMR0 = new CMR0();
                //Runtime.TargetCMR0.read(new FileData(filename));
                PARAMEditor p = new PARAMEditor(filename) { ShowHint = DockState.Document };
                p.Text = Path.GetFileName(filename);
                AddDockedControl(p);
                paramEditors.Add(p);
            }

            if (filename.EndsWith(".mdl0"))
            {
                MDL0Bones mdl0 = new MDL0Bones();
                Runtime.TargetVBN = mdl0.GetVBN(new FileData(filename));
            }

            if (filename.EndsWith(".smd"))
            {
                Runtime.TargetVBN = new VBN();
                SMD.read(filename, new SkelAnimation(), Runtime.TargetVBN);
            }

            if (filename.ToLower().EndsWith(".dae"))
            {
                DAEImportSettings m = new DAEImportSettings();
                m.ShowDialog();
                if (m.exitStatus == DAEImportSettings.Opened)
                {
                    if (Runtime.ModelContainers.Count < 1)
                        Runtime.ModelContainers.Add(new ModelContainer());

                    Collada.DAEtoNUD(filename, Runtime.ModelContainers[0]);

                    // apply settings
                    m.Apply(Runtime.ModelContainers[0].nud);
                    Runtime.ModelContainers[0].nud.MergePoly();

                    meshList.refresh();
                }
            }

            if (filename.EndsWith(".mbn"))
            {
                MBN m = new MBN();
                m.Read(filename);
                ModelContainer con = new ModelContainer();
                BCH b = new BCH();
                con.bch = b;
                b.mbn = m;
                b.Read("C:\\s\\Smash\\extract\\data\\fighter\\lucas\\Ness3DS - h00\\normal.bch");
                Runtime.ModelContainers.Add(con);
            }

            /*if (filename.EndsWith(".bch"))
            {
                ModelContainer con = new ModelContainer();
                BCH b = new BCH();
                b.Read(filename);
                con.bch = b;
                Runtime.ModelContainers.Add(con);
            }*/

            if (filename.EndsWith(".nud"))
            {
                openNud(filename);
            }

            if (filename.EndsWith(".moi"))
            {
                MOI moi = new MOI(filename);
                AddDockedControl(new MOIEditor(moi) { ShowHint = DockState.DockRight });
            }

            if (filename.EndsWith(".wrkspc"))
            {
                Workspace = new WorkspaceManager(project);
                Workspace.OpenWorkspace(filename);
            }

            if (Runtime.TargetVBN != null)
            {
                ModelContainer m = new ModelContainer();
                m.vbn = Runtime.TargetVBN;
                Runtime.ModelContainers.Add(m);

                if (filename.EndsWith(".smd"))
                {
                    m.nud = SMD.toNUD(filename);
                    meshList.refresh();
                }

                leftPanel.treeRefresh();
            }
            else
            {
                foreach (ModelContainer m in Runtime.ModelContainers)
                {
                    if (m.vbn != null)
                    {
                        Runtime.TargetVBN = Runtime.ModelContainers[0].vbn;
                        break;
                    }
                }
            }
            // Don't want to mess up the project tree if we
            // just set it up already
            if (!filename.EndsWith(".wrkspc"))
                project.fillTree();
        }

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Supported Formats(.vbn, .mdl0, .smd, .nud, .lvd, .bin, .dae, .mta, .wrkspc)|*.vbn;*.mdl0;*.smd;*.lvd;*.nud;*.mtable;*.bin;*.dae;*.dat;*.mta;*.wrkspc;*.nut;*.sb|" +
                             "Smash 4 Boneset (.vbn)|*.vbn|" +
                             "Namco Model (.nud)|*.nud|" +
                             "Smash 4 Level Data (.lvd)|*.lvd|" +
                             "NW4R Model (.mdl0)|*.mdl0|" +
                             "Source Model (.SMD)|*.smd|" +
                             "Smash 4 Parameters (.bin)|*.bin|" +
                             "Collada Model Format (.dae)|*.dae|" +
                             "All files(*.*)|*.*";

                ofd.Multiselect = true;
                // "Namco Universal Data Folder (.NUD)|*.nud|" +

                if (ofd.ShowDialog() == DialogResult.OK)
                    foreach(string filename in ofd.FileNames)
                        openFile(filename);
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string filePath in files)
                {
                    openFile(filePath);
                }
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (Smash_Forge.Update.Downloaded && MessageBox.Show($"Would you like to download the following update?\n{Smash_Forge.Update.DownloadedRelease.Name}\n{Smash_Forge.Update.DownloadedRelease.Body}", "Smash Forge Updater", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Process p = new Process();
                p.StartInfo.FileName = Path.Combine(executableDir, "updater/ForgeUpdater.exe");
                p.StartInfo.WorkingDirectory = Path.Combine(executableDir, "updater/");
                p.StartInfo.Arguments = "-i -r";
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                System.Windows.Forms.Application.Exit();
            }
        }

        private void mergeModelsMeshListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            meshList.mergeModel();
        }

        private void mergeBonesBoneListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //stub
        }
    }
}

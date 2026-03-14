using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Toolbox;
using Toolbox.Library;
using Toolbox.Library.Forms;
using Toolbox.Library.IO;
using FirstPlugin.Forms;
using Syroot.Maths;
using SharpYaml.Serialization;
using FirstPlugin;
using LayoutBXLYT.Revolution;

namespace LayoutBXLYT
{
	public class BRLYT : IFileFormat, IEditorForm<LayoutEditor>, IConvertableTextFormat, ILeaveOpenOnLoad
	{
		public FileType FileType { get; set; } = FileType.Layout;

		public bool CanSave { get; set; }
		public string[] Description { get; set; } = new string[] { "Revolution Layout (GUI)" };
		public string[] Extension { get; set; } = new string[] { "*.brlyt" };
		public string FileName { get; set; }
		public string FilePath { get; set; }
		public IFileInfo IFileInfo { get; set; }

		public bool Identify(System.IO.Stream stream)
		{
			using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
			{
				return reader.CheckSignature(4, "RLYT") ||
					   reader.CheckSignature(4, "TYLR");
			}
		}

		public Type[] Types
		{
			get
			{
				List<Type> types = new List<Type>();
				return types.ToArray();
			}
		}

		#region Text Converter Interface
		public TextFileType TextFileType => TextFileType.Xml;
		public bool CanConvertBack => false;

		public string ConvertToString()
		{
			return "";
		}

		public void ConvertFromString(string text)
		{
		}
		#endregion

		public LayoutEditor OpenForm()
		{
			LayoutEditor editor = new LayoutEditor();
			editor.Dock = DockStyle.Fill;
			editor.LoadBxlyt(header);
			return editor;
		}

		public void FillEditor(Form control)
		{
			((LayoutEditor)control).LoadBxlyt(header);
		}

		public Header header;

		public void Load(System.IO.Stream stream)
		{
			CanSave = true;

			header = new Header();
			header.Read(new FileReader(stream), this);
		}

        public Dictionary<string, STGenericTexture> GetTextures()
        {
            Dictionary<string, STGenericTexture> textures = new Dictionary<string, STGenericTexture>();
            if (IFileInfo.ArchiveParent != null)
            {
                foreach (var file in IFileInfo.ArchiveParent.Files)
                {
                    try
                    {
                        if (Utils.GetExtension(file.FileName) == ".tpl")
                        {
                            TPL tpl = (TPL)file.OpenFile();
                            file.FileFormat = tpl;
                            foreach (var tex in tpl.TextureList)
                            {
                                if (!textures.ContainsKey(tpl.FileName))
                                    textures.Add(tpl.FileName, tex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        STErrorDialog.Show($"Failed to load texture {file.FileName}. ", "Layout Editor", ex.ToString());
                    }
                }
            }
            return textures;
        }

		public void Unload()
		{
		}

		public void Save(System.IO.Stream stream)
		{
			// We implement correct BRLYT writing in Header.Write
			header.Write(new FileWriter(stream));
		}

		// Thanks to SwitchThemes for flags, and enums
		// https://github.com/FuryBaguette/SwitchLayoutEditor/tree/master/SwitchThemesCommon
		public class Header : BxlytHeader, IDisposable
		{
			private string Magic = "RLYT";

			private ushort ByteOrderMark;
			private ushort HeaderSize;

			public LYT1 LayoutInfo { get; set; }
			public TXL1 TextureList { get; set; }
			public MAT1 MaterialList { get; set; }
			public FNL1 FontList { get; set; }

			// Preserve unknown sections (round-trip friendly)
			private readonly List<RawSection> _unknownSections = new List<RawSection>();

			private class RawSection
			{
				public string Signature;
				public byte[] Payload; // excludes 8-byte section header
			}

			public override int AddFont(string name)
			{
				if (!FontList.Fonts.Contains(name))
					FontList.Fonts.Add(name);

				return FontList.Fonts.IndexOf(name);
			}

			public override int AddTexture(string name)
			{
				if (!TextureList.Textures.Contains(name))
					TextureList.Textures.Add(name);

				return TextureList.Textures.IndexOf(name);
			}

			public override void RemoveTexture(string name)
			{
				if (TextureList.Textures.Contains(name))
					TextureList.Textures.Remove(name);

				RemoveTextureReferences(name);
			}

			public override short AddMaterial(BxlytMaterial material)
			{
				if (material == null) return -1;

				if (!MaterialList.Materials.Contains(material))
					MaterialList.Materials.Add(material);

				if (material.NodeWrapper == null)
					material.NodeWrapper = new MatWrapper(material.Name)
					{
						Tag = material,
						ImageKey = "material",
						SelectedImageKey = "material",
					};

				if (!MaterialFolder.Nodes.Contains(material.NodeWrapper))
					MaterialFolder.Nodes.Add(material.NodeWrapper);

				return (short)MaterialList.Materials.IndexOf(material);
			}

			public override List<int> AddMaterial(List<BxlytMaterial> materials)
			{
				List<int> indices = new List<int>();
				foreach (var material in materials)
					indices.Add(AddMaterial(material));
				return indices;
			}

			public override void TryRemoveMaterial(BxlytMaterial material)
			{
				if (material == null) return;
				material.RemoveNodeWrapper();

				if (MaterialList.Materials.Contains(material))
					MaterialList.Materials.Remove(material);
			}

			public override void TryRemoveMaterial(List<BxlytMaterial> materials)
			{
				foreach (var material in materials)
				{
					if (material == null) continue;
					material.RemoveNodeWrapper();

					if (MaterialList.Materials.Contains(material))
						MaterialList.Materials.Remove(material);
				}
			}

			public override BasePane CreateNewNullPane(string name) => new PAN1(this, name);
			public override BasePane CreateNewTextPane(string name) => new TXT1(this, name);
			public override BasePane CreateNewPicturePane(string name) => new PIC1(this, name);
			public override BasePane CreateNewWindowPane(string name) => new WND1(this, name);
			public override BasePane CreateNewBoundryPane(string name) => new BND1(this, name);

			public override List<string> Textures => TextureList.Textures;
			public override List<string> Fonts => FontList.Fonts;
			public override List<BxlytMaterial> Materials => MaterialList.Materials;

			public override Dictionary<string, STGenericTexture> GetTextures =>
				((BRLYT)FileInfo).GetTextures();

			public override BxlytMaterial GetMaterial(ushort index) => MaterialList.Materials[index];

			public override BxlytMaterial CreateNewMaterial(string name) => new Material(name, this);

			public void Read(FileReader reader, BRLYT brlyt)
			{
				_unknownSections.Clear();

				PaneLookup.Clear();
				LayoutInfo = new LYT1();
				TextureList = new TXL1();
				MaterialList = new MAT1();
				FontList = new FNL1();
				RootPane = new PAN1();
				RootGroup = new GRP1();

				FileInfo = brlyt;

				reader.SetByteOrder(true);
				Magic = reader.ReadSignature(4);
				if (Magic == "TYLR")
					reader.ReverseMagic = true;

				ByteOrderMark = reader.ReadUInt16();
				reader.CheckByteOrderMark(ByteOrderMark);

				Version = reader.ReadUInt16();
				uint fileSize = reader.ReadUInt32();
				HeaderSize = reader.ReadUInt16();
				ushort sectionCount = reader.ReadUInt16();

				IsBigEndian = reader.ByteOrder == Syroot.BinaryData.ByteOrder.BigEndian;
				TextureManager.LayoutFile = this;
				TextureManager.Platform = TextureManager.PlatformType.Wii;

				bool setRoot = false;
				bool setGroupRoot = false;

				BasePane currentPane = null;
				BasePane parentPane = null;

				GroupPane currentGroupPane = null;
				GroupPane parentGroupPane = null;

				reader.SeekBegin(HeaderSize);

				for (int i = 0; i < sectionCount; i++)
				{
					long sectionStart = reader.Position;

					string signature = reader.ReadSignature(4);
					uint sectionSize = reader.ReadUInt32();

					// sectionSize includes the 8-byte section header per spec
					uint payloadSize = (sectionSize >= 8) ? (sectionSize - 8) : 0;

					switch (signature)
					{
						case "lyt1":
							LayoutInfo = new LYT1(reader);
							break;
						case "txl1":
							TextureList = new TXL1(reader, this);
							break;
						case "fnl1":
							FontList = new FNL1(reader, this);
							break;
						case "mat1":
							MaterialList = new MAT1(reader, this);
							break;

						case "pan1":
							{
								var panel = new PAN1(reader, this);
								AddPaneToTable(panel);
								if (!setRoot)
								{
									RootPane = panel;
									setRoot = true;
								}
								SetPane(panel, parentPane);
								currentPane = panel;
							}
							break;

						case "pic1":
							{
								var picturePanel = new PIC1(reader, this);
								AddPaneToTable(picturePanel);
								SetPane(picturePanel, parentPane);
								currentPane = picturePanel;
							}
							break;

						case "txt1":
							{
								var textPanel = new TXT1(reader, this);
								AddPaneToTable(textPanel);
								SetPane(textPanel, parentPane);
								currentPane = textPanel;
							}
							break;

						case "bnd1":
							{
								var boundsPanel = new BND1(reader, this);
								AddPaneToTable(boundsPanel);
								SetPane(boundsPanel, parentPane);
								currentPane = boundsPanel;
							}
							break;

						case "wnd1":
							{
								var windowPanel = new WND1(this, reader);
								AddPaneToTable(windowPanel);
								SetPane(windowPanel, parentPane);
								currentPane = windowPanel;
							}
							break;

						case "pas1":
							if (currentPane != null)
								parentPane = currentPane;
							break;

						case "pae1":
							currentPane = parentPane;
							parentPane = currentPane?.Parent;
							break;

						case "grp1":
							{
								var groupPanel = new GRP1(reader, this);
								if (!setGroupRoot)
								{
									RootGroup = groupPanel;
									setGroupRoot = true;
								}
								SetPane(groupPanel, parentGroupPane);
								currentGroupPane = groupPanel;
							}
							break;

						case "grs1":
							if (currentGroupPane != null)
								parentGroupPane = currentGroupPane;
							break;

						case "gre1":
							currentGroupPane = parentGroupPane;
							parentGroupPane = currentGroupPane?.Parent;
							break;

						case "usd1":
							// If your existing IUserDataContainer parsing happens elsewhere, leave it.
							// Otherwise, you can parse usd1 here. We still must NOT count usd1 in header count when writing.
							// Consume payload if constructors didn't.
							reader.ReadBytes((int)payloadSize);
							break;

						default:
							// Correctly preserve only the payload bytes (not sectionSize again)
							_unknownSections.Add(new RawSection
							{
								Signature = signature,
								Payload = reader.ReadBytes((int)payloadSize),
							});
							break;
					}

					// Seek exactly to the next section start (authoritative)
					reader.SeekBegin(sectionStart + sectionSize);
				}
			}

			private void SetPane(GroupPane pane, GroupPane parentPane)
			{
				if (parentPane != null)
				{
					parentPane.Childern.Add(pane);
					pane.Parent = parentPane;
				}
			}

			private void SetPane(BasePane pane, BasePane parentPane)
			{
				if (parentPane != null)
				{
					parentPane.Childern.Add(pane);
					pane.Parent = parentPane;
				}
			}

			// ----------------------------
			// Correct BRLYT writer per spec
			// ----------------------------

			private static void Align4(FileWriter writer)
			{
				long pos = writer.Position;
				long pad = (4 - (pos % 4)) % 4;
				for (int i = 0; i < pad; i++)
					writer.Write((byte)0);
			}

			private void WriteSection(FileWriter writer, string signature, Action writePayload)
			{
				long start = writer.Position;

				writer.WriteSignature(signature);

				// Reserve section size, patch later (size includes 8-byte header)
				long sizePos = writer.Position;
				writer.Write(uint.MaxValue);

				writePayload?.Invoke();

				Align4(writer);

				long end = writer.Position;
				uint sectionSize = (uint)(end - start);

				using (writer.TemporarySeek(sizePos, System.IO.SeekOrigin.Begin))
				{
					writer.Write(sectionSize);
				}
			}

			private void WriteMarkerSection(FileWriter writer, string signature)
			{
				// Marker sections are header-only: size = 8
				writer.WriteSignature(signature);
				writer.Write((uint)8);
				Align4(writer);
			}

			public void Write(FileWriter writer)
			{
				RecalculateMaterialReferences();

				writer.SetByteOrder(IsBigEndian);

				// ---- File Header (0x10) ----
				writer.WriteSignature(Magic);
				if (Magic == "TYLR")
					writer.ReverseMagic = true;

				writer.Write(ByteOrderMark);
				writer.Write((ushort)Version);

				// reserve file size
				long fileSizePos = writer.Position;
				writer.Write(uint.MaxValue);

				// header size (spec says 0x10 for BRLYT)
				writer.Write((ushort)0x10);

				// reserve section count (excludes usd1 per spec)
				long sectionCountPos = writer.Position;
				writer.Write(ushort.MaxValue);

				int sectionCountExcludingUsd1 = 0;

				// ---- Sections ----

				// lyt1 always first
				WriteSection(writer, "lyt1", () => LayoutInfo.Write(writer, this));
				sectionCountExcludingUsd1++;

				// Resource sections in order (only if needed)
				if (TextureList != null && TextureList.Textures.Count > 0)
				{
					WriteSection(writer, "txl1", () => TextureList.Write(writer, this));
					sectionCountExcludingUsd1++;
				}
				if (FontList != null && FontList.Fonts.Count > 0)
				{
					WriteSection(writer, "fnl1", () => FontList.Write(writer, this));
					sectionCountExcludingUsd1++;
				}
				if (MaterialList != null && MaterialList.Materials.Count > 0)
				{
					WriteSection(writer, "mat1", () => MaterialList.Write(writer, this));
					sectionCountExcludingUsd1++;
				}

				// Panes + user data sections (usd1 NOT counted)
				WritePanes(writer, RootPane, this, ref sectionCountExcludingUsd1);

				// Groups
				WriteGroupPanes(writer, RootGroup, this, ref sectionCountExcludingUsd1);

				// If you want to preserve unknown sections, you can append them here.
				// (If ordering matters for your game, we can upgrade this to preserve original order exactly.)
				foreach (var raw in _unknownSections)
				{
					WriteSection(writer, raw.Signature, () => writer.Write(raw.Payload));
					sectionCountExcludingUsd1++;
				}

				// ---- Patch header fields ----
				using (writer.TemporarySeek(sectionCountPos, System.IO.SeekOrigin.Begin))
				{
					writer.Write((ushort)sectionCountExcludingUsd1);
				}

				using (writer.TemporarySeek(fileSizePos, System.IO.SeekOrigin.Begin))
				{
					writer.Write((uint)writer.BaseStream.Length);
				}
			}

			private void WritePanes(FileWriter writer, BasePane pane, LayoutHeader header, ref int sectionCountExcludingUsd1)
			{
				WriteSection(writer, pane.Signature, () => pane.Write(writer, header));
				sectionCountExcludingUsd1++;

				if (pane is IUserDataContainer && ((IUserDataContainer)pane).UserData != null)
				{
					var userData = ((IUserDataContainer)pane).UserData;

					// usd1 exists as a section, but is NOT included in header section count per spec
					WriteSection(writer, "usd1", () => userData.Write(writer, this));
				}

				if (pane.HasChildern)
				{
					// pas1 marker (counted)
					WriteMarkerSection(writer, "pas1");
					sectionCountExcludingUsd1++;

					foreach (var child in pane.Childern)
						WritePanes(writer, child, header, ref sectionCountExcludingUsd1);

					// pae1 marker (counted)
					WriteMarkerSection(writer, "pae1");
					sectionCountExcludingUsd1++;
				}
			}

			private void WriteGroupPanes(FileWriter writer, GroupPane pane, LayoutHeader header, ref int sectionCountExcludingUsd1)
			{
				WriteSection(writer, pane.Signature, () => pane.Write(writer, header));
				sectionCountExcludingUsd1++;

				if (pane.HasChildern)
				{
					WriteMarkerSection(writer, "grs1");
					sectionCountExcludingUsd1++;

					foreach (var child in pane.Childern)
						WriteGroupPanes(writer, child, header, ref sectionCountExcludingUsd1);

					WriteMarkerSection(writer, "gre1");
					sectionCountExcludingUsd1++;
				}
			}

			public void Dispose()
			{
			}
		}
	}
}
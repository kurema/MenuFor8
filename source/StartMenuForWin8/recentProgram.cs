﻿//------------------------------------------------------------------------------
// <auto-generated>
//     このコードはツールによって生成されました。
//     ランタイム バージョン:4.0.30319.261
//
//     このファイルへの変更は、以下の状況下で不正な動作の原因になったり、
//     コードが再生成されるときに損失したりします。
// </auto-generated>
//------------------------------------------------------------------------------

// 
// このソース コードは xsd によって自動生成されました。Version=4.0.30319.1 です。
// 
namespace XmlRecentProgram {
    using System.Xml.Serialization;
    
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true, Namespace="http://toro.2ch.net/test/read.cgi/win/1330826421/sm4win8.recentProgram.xsd")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace="http://toro.2ch.net/test/read.cgi/win/1330826421/sm4win8.recentProgram.xsd", IsNullable=false)]
    public partial class recent {
        
        private recentProgram[] itemsField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("program")]
        public recentProgram[] Items {
            get {
                return this.itemsField;
            }
            set {
                this.itemsField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true, Namespace="http://toro.2ch.net/test/read.cgi/win/1330826421/sm4win8.recentProgram.xsd")]
    public partial class recentProgram {
        
        private string hrefField;
        
        private string pointField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string href {
            get {
                return this.hrefField;
            }
            set {
                this.hrefField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute(DataType="nonNegativeInteger")]
        public string point {
            get {
                return this.pointField;
            }
            set {
                this.pointField = value;
            }
        }
    }
}